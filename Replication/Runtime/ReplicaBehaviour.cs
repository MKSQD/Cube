using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System;
using Cube.Transport;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
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
