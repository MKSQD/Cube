using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System;
using Cube.Networking.Transport;
using BitStream = Cube.Networking.Transport.BitStream;

namespace Cube.Networking.Replicas {
    public enum ReplicaSerializationMode {
        Partial = 0,
        Full = 1
    }

    /// <summary>
    /// 
    /// </summary>
    public abstract class ReplicaBehaviour : NetworkBehaviour {
        [HideInInspector]
        public byte replicaComponentIdx;

        [HideInInspector]
        public ulong dirtyFieldsMask;

        protected Dictionary<byte, MethodInfo> _rpcMethods = new Dictionary<byte, MethodInfo>();
        public Dictionary<byte, MethodInfo> rpcMethods {
            get { return _rpcMethods; }
        }

        Replica _replica;
        public Replica replica {
            get {
                if (_replica == null) {
                    _replica = GetComponentInParent<Replica>();
                }
                return _replica;
            }
        }

#if SERVER
        public List<BitStream> rpcs = new List<BitStream>();

        static Connection _rpcConnection = Connection.Invalid;

        /// <summary>
        /// Default: null
        /// In RPC-Method: sender Connection
        /// </summary>
        protected Connection rpcConnection {
            get { return _rpcConnection; }
        }
#endif

#if SERVER
        public virtual void Serialize(BitStream bs, ReplicaSerializationMode mode, ReplicaView view) { }
#endif

        public virtual void Deserialize(BitStream bs, ReplicaSerializationMode mode) { }

        public void SendRpc(byte methodId, params object[] args) {
#if CLIENT
            if (isClient) {
                var client = replica.client;

                var bs = client.networkInterface.bitStreamPool.Create();
                bs.Write((byte)MessageId.ReplicaRpc);
                bs.Write(replica.id);
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
                bs.Write(replica.id);
                bs.Write(replicaComponentIdx);
                bs.Write(methodId);

                for (int i = 0; i < args.Length; ++i) {
                    WriteValueToBitStream(args[i], bs);
                }

                rpcs.Add(bs);
            }
#endif
        }

        public static void CallRpc(Replica replica, Connection connection, BitStream bs, IReplicaManager replicaManager) {
            var componentIdx = bs.ReadByte();
            var methodId = bs.ReadByte();

            var component = replica.replicaBehaviours[componentIdx];

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

#if SERVER
            _rpcConnection = connection;
#endif

            methodInfo.Invoke(component, args);

#if SERVER
            _rpcConnection = Connection.Invalid;
#endif
        }

        void WriteValueToBitStream(object value, BitStream bs) {
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
        
        // Do not remove, the call sites will automatically be patched by the AssemblyPatcher
        protected bool HasReplicaVarChanged<T>(T field) {
            return false;
        }
    }
}
