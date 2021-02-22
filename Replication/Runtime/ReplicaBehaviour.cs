using Cube.Transport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    public abstract class ReplicaBehaviour : MonoBehaviour {
        public struct SerializeContext {
            public ReplicaView Observer;
        }

        [HideInInspector]
        public byte replicaComponentIdx;

        [HideInInspector]
        public ulong dirtyFieldsMask;

        protected Dictionary<byte, MethodInfo> _rpcMethods = new Dictionary<byte, MethodInfo>();
        public Dictionary<byte, MethodInfo> rpcMethods {
            get { return _rpcMethods; }
        }
        
        [HideInInspector]
        public Replica Replica;

        public static Connection rpcConnection = Connection.Invalid;

        public ICubeServer server {
            get { return Replica.server; }
        }

        public ICubeClient client {
            get { return Replica.client; }
        }

        public bool isServer {
            get { return Replica.isServer; }
        }

        public bool isClient {
            get { return Replica.isClient; }
        }

        public bool isOwner {
            get { return Replica.isOwner; }
        }

        public virtual void Serialize(BitStream bs, SerializeContext ctx) { }
        public virtual void Deserialize(BitStream bs) { }

        public virtual void SerializeDestruction(BitStream bs, SerializeContext ctx) { }
        public virtual void DeserializeDestruction(BitStream bs) { }

        // Do not remove, the call sites will automatically be patched by the AssemblyPatcher
        protected bool HasReplicaVarChanged<T>(T field) {
            return false;
        }
    }
}
