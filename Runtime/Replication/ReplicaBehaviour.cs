using Cube.Transport;
using UnityEngine;


namespace Cube.Replication {
    public abstract class BaseReplicaBehaviour : MonoBehaviour {
        [HideInInspector]
        public Replica Replica;

        public bool isServer => Replica.isServer;
        public bool isClient => Replica.isClient;
        public bool IsOwned => Replica.IsOwner;

        public ICubeServer server => Replica.server;
        public ICubeClient client => Replica.client;
    }

    public abstract class ReplicaBehaviour : BaseReplicaBehaviour {
        public struct SerializeContext {
            /// <summary>
            /// Is the current Serialize call for the owner of this Replica?
            /// </summary>
            public bool IsOwner;
        }

        [HideInInspector]
        public byte ReplicaComponentIdx;

        public static Connection RpcConnection = Connection.Invalid;

        public IReplicaManager ReplicaManager => Replica.ReplicaManager;

        public virtual void Serialize(IBitWriter bs, SerializeContext ctx) { }
        public virtual void Deserialize(BitReader bs) { }

        /// Do NOT manually overwrite this. RpcPatcher will generate this dispatch table automatically.
        public virtual void DispatchRpc(byte methodIdx, BitReader bs) => throw new System.Exception("pure virtual call");
    }
}
