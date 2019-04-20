using Cube.Transport;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    [AddComponentMenu("Cube/Replica")]
    [DisallowMultipleComponent]
    public sealed class Replica : MonoBehaviour {
#if SERVER
        public struct QueuedRpc {
            public RpcTarget target;
            public BitStream bs;
        }
#endif

        public ReplicaSettings settings;
        public bool replicateOnlyToOwner;

        [HideInInspector]
        public ReplicaId id = ReplicaId.Invalid;

        [HideInInspector]
        public ushort prefabIdx;
        [HideInInspector]
        public byte sceneIdx;

        public bool isSceneReplica {
            get { return sceneIdx != 0; }
        }

#if SERVER
        public IUnityServer server;
#endif
#if CLIENT
        public IUnityClient client;
#endif

        public bool isServer {
            get {
#if SERVER
                return server != null;
#else
                return false;
#endif
            }
        }

        public bool isClient {
            get {
#if CLIENT
                return client != null;
#else
                return false;
#endif
            }
        }

#if SERVER
        public Connection owner {
            get;
            internal set;
        }
#endif

        public bool isOwner = false;

        public ReplicaBehaviour[] replicaBehaviours {
            get;
            internal set;
        }

#if CLIENT
        [HideInInspector]
        public float lastUpdateTime;
#endif

#if SERVER
        public List<QueuedRpc> queuedRpcs = new List<QueuedRpc>();
#endif

        static bool _applicationQuitting;

#if SERVER
        public void AssignOwnership(Connection owner) {
            Assert.IsTrue(owner != Connection.Invalid);

            this.owner = owner;
            isOwner = false;
        }

        public void TakeOwnership() {
            owner = Connection.Invalid;
            isOwner = true;
        }
        
        public bool IsRelevantFor(ReplicaView view) {
            if (!gameObject.activeInHierarchy)
                return false;

            if (replicateOnlyToOwner)
                return view.connection == owner;

            return true;
        }
#endif

        /// <summary>
        /// Removes the Replica instantly from the ReplicaManager, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// @see ServerReplicaManager.DestroyReplica
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        public void Destroy() {
            Assert.IsTrue(isServer);
#if SERVER
            server.replicaManager.DestroyReplica(this);
#endif
        }

        public void RebuildReplicaBehaviourCache() {
            replicaBehaviours = GetComponentsInChildren<ReplicaBehaviour>();
            for (byte i = 0; i < replicaBehaviours.Length; ++i) {
                var behaviour = replicaBehaviours[i];
                behaviour.replica = this;
                behaviour.replicaComponentIdx = i;
            }
        }

        void Awake() {
            RebuildReplicaBehaviourCache();
        }

        /// <summary>
        /// Removes the Replica from all global managers. Does NOT broadcast its destruction.
        /// </summary>
        void OnDestroy() {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return; // No need to remove this if not in play mode
#endif
            if (_applicationQuitting)
                return;
#if SERVER
            if (isServer)
                server.replicaManager.RemoveReplica(this);
#endif
#if CLIENT
            if (isClient)
                client.replicaManager.RemoveReplica(this);
#endif
        }

        void OnApplicationQuit() {
            _applicationQuitting = true;
        }

        public void SendRpc(byte methodId, byte replicaComponentIdx, RpcTarget target, params object[] args) {
#if CLIENT
            if (isClient) {
                var bs = client.networkInterface.bitStreamPool.Create();
                bs.Write((byte)MessageId.ReplicaRpc);
                bs.Write(id);
                bs.Write(replicaComponentIdx);
                bs.Write(methodId);

                for (int i = 0; i < args.Length; ++i) {
                    WriteValueToBitStream(args[i], bs);
                }

                client.networkInterface.Send(bs, PacketPriority.Immediate, PacketReliability.Unreliable);
            }
#endif
#if SERVER
            if (isServer) {
                var bs = new BitStream(); // #todo need to pool these instances, but lifetime could be over one frame
                bs.Write((byte)MessageId.ReplicaRpc);
                bs.Write(id);
                bs.Write(replicaComponentIdx);
                bs.Write(methodId);

                for (int i = 0; i < args.Length; ++i) {
                    WriteValueToBitStream(args[i], bs);
                }

                var qrpc = new QueuedRpc() {
                    bs = bs,
                    target = target
                };
                queuedRpcs.Add(qrpc);
            }
#endif
        }

#if SERVER
        public void CallRpcServer(Connection connection, BitStream bs, IReplicaManager replicaManager) {
            if (owner != connection) {
                var componentIdx = bs.ReadByte();
                var methodId = bs.ReadByte();

                var component = replicaBehaviours[componentIdx];

                MethodInfo methodInfo;
                if (!component.rpcMethods.TryGetValue(methodId, out methodInfo)) {
                    Debug.LogError("Cannot find rpc method with id " + methodId + " in " + component + " on " + (component.isServer ? "server" : "client") + ".");
                    return;
                }
                
                Debug.LogWarning("Got Replica rpc from non-owning client replica=" + gameObject + " method=" + methodInfo.Name);
                return;
            }

            CallRpc(connection, bs, replicaManager);
        }
#endif

        public void CallRpcClient(BitStream bs, IReplicaManager replicaManager) {
            CallRpc(Connection.Invalid, bs, replicaManager);
        }

        void CallRpc(Connection connection, BitStream bs, IReplicaManager replicaManager) {
            // #todo expose connection; maybe require first RPC arg to be ReplicaRpcContext?
            
            var componentIdx = bs.ReadByte();
            var methodId = bs.ReadByte();

            var component = replicaBehaviours[componentIdx];

            MethodInfo methodInfo;
            if (!component.rpcMethods.TryGetValue(methodId, out methodInfo)) {
                Debug.LogError("Cannot find rpc method with id " + methodId + " in " + component + " on " + (component.isServer ? "server" : "client") + ".");
                return;
            }
            
            var methodParameters = methodInfo.GetParameters();
            var args = new object[methodParameters.Length];

            for (int i = 0; i < args.Length; i++) {
                var paramType = methodParameters[i].ParameterType;
                ReadParameterFromBitStream(paramType, bs, replicaManager, out args[i]);
            }

            methodInfo.Invoke(component, args);
        }

        static void WriteValueToBitStream(object value, BitStream bs) {
            //TODO double, param object[]

            var type = value.GetType();

            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            if (type.IsArray) {
                var arrayValue = (Array)value;

                var arrayLength = (byte)arrayValue.Length;
                if (arrayValue.Length > 255) {
                    Debug.LogError("Array size may not be larger than 255");
                    arrayLength = 255;
                }
                bs.Write(arrayLength);
                for (byte i = 0; i < arrayLength; ++i) {
                    WriteValueToBitStream(arrayValue.GetValue(i), bs);
                }
                return;
            }

            if (type == typeof(bool)) {
                bs.Write((bool)value);
            }
            else if (type == typeof(byte)) {
                bs.Write((byte)value);
            }
            else if (type == typeof(ushort)) {
                bs.Write((ushort)value);
            }
            else if (type == typeof(int)) {
                bs.Write((int)value);
            }
            else if (type == typeof(long)) {
                bs.Write((long)value);
            }
            else if (type == typeof(ulong)) {
                bs.Write((ulong)value);
            }
            else if (type == typeof(float)) {
                bs.Write((float)value);
            }
            else if (type == typeof(string)) {
                bs.Write((string)value);
            }
            else if (type == typeof(Connection)) {
                bs.Write((Connection)value);
            }
            else if (type == typeof(Vector2)) {
                bs.Write((Vector2)value);
            }
            else if (type == typeof(Vector3)) {
                bs.Write((Vector3)value);
            }
            else if (type == typeof(Quaternion)) {
                bs.Write((Quaternion)value);
            }
            else if (type == typeof(ReplicaId)) {
                var replicaId = (ReplicaId)value;
                bs.Write(replicaId);
            }
            else if (type == typeof(Replica)) {
                var replica = (Replica)value;
                bs.Write(replica.id);
            }
            else if (type.IsSubclassOf(typeof(NetworkObject))) {
                bs.WriteNetworkObject((NetworkObject)value);
            }
            else {
                var obj = value as ISerializable;
                if (obj != null) {
                    obj.Serialize(bs);
                }
                else {
                    Debug.LogError("Cannot serialize rpc argument of type " + value.GetType());
                }
            }
        }

        static void ReadParameterFromBitStream(Type type, BitStream bs, IReplicaManager replicaManager, out object value) {
            // #TODO double, param object[]

            if (type.IsEnum)
                type = Enum.GetUnderlyingType(type);

            if (type.IsArray) {
                var length = bs.ReadByte();

                var newArray = (Array)Activator.CreateInstance(type, new object[] { (int)length });
                for (byte i = 0; i < length; ++i) {
                    object elementValue;
                    ReadParameterFromBitStream(type.GetElementType(), bs, replicaManager, out elementValue);
                    newArray.SetValue(elementValue, i);
                }
                value = newArray;
                return;
            }

            if (type == typeof(bool)) {
                value = bs.ReadBool();
            }
            else if (type == typeof(byte)) {
                value = bs.ReadByte();
            }
            else if (type == typeof(ushort)) {
                value = bs.ReadUShort();
            }
            else if (type == typeof(int)) {
                value = bs.ReadInt();
            }
            else if (type == typeof(long)) {
                value = bs.ReadLong();
            }
            else if (type == typeof(ulong)) {
                value = bs.ReadULong();
            }
            else if (type == typeof(float)) {
                value = bs.ReadFloat();
            }
            else if (type == typeof(string)) {
                value = bs.ReadString();
            }
            else if (type == typeof(Connection)) {
                value = bs.ReadConnection();
            }
            else if (type == typeof(Vector2)) {
                value = bs.ReadVector2();
            }
            else if (type == typeof(Vector3)) {
                value = bs.ReadVector3();
            }
            else if (type == typeof(Quaternion)) {
                value = bs.ReadQuaternion();
            }
            else if (type == typeof(ReplicaId)) {
                value = bs.ReadReplicaId();
            }
            else if (type == typeof(Replica)) {
                var id = bs.ReadReplicaId();
                
                value = replicaManager.GetReplicaById(id);
            }
            else if (type.IsSubclassOf(typeof(NetworkObject))) {
                value = bs.ReadNetworkObject<NetworkObject>();
            }
            else {
                var obj = Activator.CreateInstance(type) as ISerializable;
                if (obj != null) {
                    obj.Deserialize(bs);
                    value = obj;
                }
                else {
                    value = null;
                    Debug.LogError("Cannot deserialize rpc argument of type " + type);
                }
            }
        }
    }
}
