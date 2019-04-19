using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    public enum ReplicaSerializationMode {
        Partial = 0,
        Full = 1
    }

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

#if SERVER
        public IUnityServer server {
            get { return replica.server; }
        }
#endif
#if CLIENT
        public IUnityClient client {
            get { return replica.client; }
        }
#endif

        public bool isServer {
            get { return replica.isServer; }
        }

        public bool isClient {
            get { return replica.isClient; }
        }

        public bool isOwner {
            get { return replica.isOwner; }
        }

#if SERVER
        public virtual void Serialize(BitStream bs, ReplicaSerializationMode mode, ReplicaView view) { }
#endif

        public virtual void Deserialize(BitStream bs, ReplicaSerializationMode mode) { }
        
        // Do not remove, the call sites will automatically be patched by the AssemblyPatcher
        protected bool HasReplicaVarChanged<T>(T field) {
            return false;
        }
    }
}
