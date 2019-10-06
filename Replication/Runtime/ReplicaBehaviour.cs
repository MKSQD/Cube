using Cube.Transport;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <summary>
    /// 
    /// </summary>
    public abstract class ReplicaBehaviour : MonoBehaviour {
        [HideInInspector]
        public byte replicaComponentIdx;

        [HideInInspector]
        public ulong dirtyFieldsMask;

        protected Dictionary<byte, MethodInfo> _rpcMethods = new Dictionary<byte, MethodInfo>();
        public Dictionary<byte, MethodInfo> rpcMethods {
            get { return _rpcMethods; }
        }
        
        [HideInInspector]
        public Replica replica;

        public static Connection rpcConnection = Connection.Invalid;

        public ICubeServer server {
            get { return replica.server; }
        }

        public ICubeClient client {
            get { return replica.client; }
        }

        public bool isServer {
            get { return replica.isServer; }
        }

        public bool isClient {
            get { return replica.isClient; }
        }

        public bool isOwner {
            get { return replica.isOwner; }
        }

        public virtual void Serialize(BitStream bs, ReplicaView view) { }
        public virtual void Deserialize(BitStream bs) { }

        public virtual void SerializeDestruction(BitStream bs, ReplicaView view) { }
        public virtual void DeserializeDestruction(BitStream bs) { }

        // Do not remove, the call sites will automatically be patched by the AssemblyPatcher
        protected bool HasReplicaVarChanged<T>(T field) {
            return false;
        }
    }
}
