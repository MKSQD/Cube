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
    public class Replica : MonoBehaviour {
        public struct QueuedRpc {
            public RpcTarget target;
            public BitStream bs;
        }

        public static ReplicaSettings defaultReplicaSettings;
        public ReplicaSettings settings;
        public ReplicaSettings settingsOrDefault {
            get {
                return settings != null ? settings : defaultReplicaSettings;
            }
        }

        public bool replicateOnlyToOwner;

        [HideInInspector]
        public ReplicaId ReplicaId = ReplicaId.Invalid;

        [HideInInspector]
        public ushort prefabIdx;
        [HideInInspector]
        public byte sceneIdx;

        public bool isSceneReplica {
            get { return sceneIdx != 0; }
        }

        public ICubeServer server;
        public ICubeClient client;

        public bool isServer {
            get {
                return server != null;
            }
        }

        public bool isClient {
            get {
                return client != null;
            }
        }

        public Connection Owner {
            get;
            internal set;
        }

        public bool isOwner {
            get;
            internal set;
        }

        ReplicaBehaviour[] _replicaBehaviours;

        /// <summary>
        /// Used on the client to remove Replicas which received no updates for a long time.
        /// </summary>
        [HideInInspector]
        public float lastUpdateTime;

        public List<QueuedRpc> queuedRpcs = new List<QueuedRpc>();

        static bool _applicationQuitting;

        public void AssignOwnership(Connection owner) {
            Assert.IsTrue(isServer);
            Assert.IsTrue(owner != Connection.Invalid);

            Owner = owner;
            isOwner = false;
        }

        public void TakeOwnership() {
            Assert.IsTrue(isServer);

            Owner = Connection.Invalid;
            isOwner = true;
        }

        public void ClientUpdateOwnership(bool owned) {
            if (owned == isOwner)
                return;

            isOwner = owned;
        }

        public bool IsRelevantFor(ReplicaView view) {
            Assert.IsTrue(isServer);

            if (!gameObject.activeInHierarchy)
                return false;

            if (replicateOnlyToOwner)
                return view.Connection == Owner;

            return true;
        }

        /// [0,1]
        public virtual float GetRelevance(ReplicaView view) {
            Assert.IsNotNull(view);
            Assert.IsTrue(isServer);

            var distanceRelevance = 1f;
            if ((settings.priorityFlags & ReplicaPriorityFlag.IgnorePosition) == 0 && !view.IgnoreReplicaPositionsForPriority) {
                var sqrMaxDist = Mathf.Pow(settings.MaxViewDistance, 2);

                var sqrDist = Mathf.Pow(transform.position.x - view.transform.position.x, 2)
                    + Mathf.Pow(transform.position.z - view.transform.position.z, 2);
                if (sqrDist > sqrMaxDist)
                    return 0; // No costly calculations

                distanceRelevance = 1f - Mathf.Pow(sqrDist / sqrMaxDist, 0.8f);
            }
            return distanceRelevance;
        }

        /// <summary>
        /// SERVER only. Removes the Replica instantly from replication, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// </summary>
        public void Destroy() {
            if (!isServer)
                return;

            server.replicaManager.DestroyReplica(this);
        }

        /// <summary>
        /// SERVER only. Removes the Replica instantly from replication. Does NOT send any message to the clients.
        /// </summary>
        /// <remarks>
        /// Use this when you want to remove the Replica yourself via an RPC. This way you can send additional information to the clients.
        /// </remarks>
        public void Remove() {
            if (!isServer)
                return;

            server.replicaManager.RemoveReplica(this);
        }

        public void Serialize(BitStream bs, ReplicaBehaviour.SerializeContext ctx) {
            foreach (var component in _replicaBehaviours) {
                component.Serialize(bs, ctx);
            }
        }

        public void Deserialize(BitStream bs) {
            foreach (var component in _replicaBehaviours) {
                component.Deserialize(bs);
            }
        }

        public void SerializeDestruction(BitStream bs, ReplicaBehaviour.SerializeContext ctx) {
            foreach (var component in _replicaBehaviours) {
                component.SerializeDestruction(bs, ctx);
            }
        }

        public void DeserializeDestruction(BitStream bs) {
            foreach (var component in _replicaBehaviours) {
                component.DeserializeDestruction(bs);
            }
        }

        public void RebuildCaches() {
            _replicaBehaviours = GetComponentsInChildren<ReplicaBehaviour>();

            byte idx = 0;
            foreach (var rb in _replicaBehaviours) {
                rb.replica = this;
                rb.replicaComponentIdx = idx++;
            }
        }

        void Awake() {
            if (settings == null) {
                if (defaultReplicaSettings == null) {
                    defaultReplicaSettings = ScriptableObject.CreateInstance<ReplicaSettings>();
                }

                settings = defaultReplicaSettings;
            }

            RebuildCaches();
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

            if (isServer)
                server.replicaManager.RemoveReplica(this);

            if (isClient)
                client.replicaManager.RemoveReplica(this);
        }

        void OnApplicationQuit() {
            _applicationQuitting = true;
        }

        public void SendRpc(byte methodId, byte componentIdx, RpcTarget target, params object[] args) {
            if (isClient) {
                var bs = client.networkInterface.bitStreamPool.Create();
                bs.Write((byte)MessageId.ReplicaRpc);
                bs.Write(ReplicaId);
                bs.Write(componentIdx);
                bs.Write(methodId);

                for (int i = 0; i < args.Length; ++i) {
                    WriteValueToBitStream(args[i], bs);
                }

                client.networkInterface.Send(bs, PacketPriority.Medium, PacketReliability.Unreliable);
            }

            if (isServer) {
                var bs = new BitStream(); // #todo need to pool these instances, but lifetime could be over one frame
                bs.Write((byte)MessageId.ReplicaRpc);
                bs.Write(ReplicaId);
                bs.Write(componentIdx);
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
        }

        public void CallRpcServer(Connection connection, BitStream bs, IReplicaManager replicaManager) {
            var replicaOwnedByCaller = Owner == connection;
            if (!replicaOwnedByCaller) {
                var componentIdx = bs.ReadByte();
                var methodId = bs.ReadByte();

                var component = _replicaBehaviours[componentIdx];

                MethodInfo methodInfo;
                if (!component.rpcMethods.TryGetValue(methodId, out methodInfo)) {
                    Debug.LogError("Cannot find RPC method with id " + methodId + " in " + component + " on " + (component.isServer ? "server" : "client") + ".");
                    return;
                }

#if CUBE_DEBUG_REP
                var ownerStr = Owner != Connection.Invalid ? Owner.ToString() : "Server";
                Debug.LogWarning("Got Replica RPC from non-owning client. Replica=" + gameObject.name + " Method=" + methodInfo.Name + " Client=" + connection + " Owner=" + ownerStr, gameObject);
#endif
                return;
            }

            ReplicaBehaviour.rpcConnection = connection;
            try {
                CallRpcImpl(connection, bs, replicaManager);
            }
            finally {
                ReplicaBehaviour.rpcConnection = Connection.Invalid;
            }
        }

        public void CallRpcClient(BitStream bs, IReplicaManager replicaManager) {
            CallRpcImpl(Connection.Invalid, bs, replicaManager);
        }

        void CallRpcImpl(Connection connection, BitStream bs, IReplicaManager replicaManager) {
            // #todo expose connection; maybe require first RPC arg to be ReplicaRpcContext?

            var componentIdx = bs.ReadByte();
            var methodId = bs.ReadByte();

            var component = _replicaBehaviours[componentIdx];

            MethodInfo methodInfo;
            if (!component.rpcMethods.TryGetValue(methodId, out methodInfo)) {
                Debug.LogError("Cannot find rpc method with id " + methodId + " in " + component + " on " + (component.isServer ? "server" : "client") + ".");
                return;
            }

            var methodParameters = methodInfo.GetParameters();
            var args = new object[methodParameters.Length];

            for (int i = 0; i < args.Length; i++) {
                var paramType = methodParameters[i].ParameterType;
                if (!TryReadParameterFromBitStream(paramType, bs, replicaManager, out args[i])) {
                    Debug.LogWarning("Dropped Replica RPC method=" + methodInfo.Name, this);
                    Debug.LogWarning("Method Idx = " + methodId);
                    Debug.LogWarning("componentIdx = " + componentIdx);
                    return;
                }
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
            else if (type == typeof(uint)) {
                bs.Write((uint)value);
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
                bs.Write(replica.ReplicaId);
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

        static bool TryReadParameterFromBitStream(Type type, BitStream bs, IReplicaManager replicaManager, out object value) {
            // #TODO double, param object[]

            if (type.IsEnum) {
                type = Enum.GetUnderlyingType(type);
            }

            if (type.IsArray) {
                var length = bs.ReadByte();

                var newArray = (Array)Activator.CreateInstance(type, new object[] { (int)length });
                for (byte i = 0; i < length; ++i) {
                    if (!TryReadParameterFromBitStream(type.GetElementType(), bs, replicaManager, out object elementValue)) {
                        value = null;
                        return false;
                    }

                    newArray.SetValue(elementValue, i);
                }

                value = newArray;
                return true;
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
            else if (type == typeof(uint)) {
                value = bs.ReadUInt();
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
                if (value == null) {
                    Debug.LogWarning("RPC was dropped because Replica (used as argument) was not found: " + id);
                    return false;
                }
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
            return true;
        }
    }
}
