using Cube.Transport;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    public abstract class BaseReplicaBehaviour : MonoBehaviour {
        [HideInInspector]
        public Replica Replica;

        public bool isServer => Replica.isServer;
        public bool isClient => Replica.isClient;
        public bool isOwner => Replica.IsOwner;

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
        public byte replicaComponentIdx;

        public static Connection rpcConnection = Connection.Invalid;

        public IReplicaManager ReplicaManager => Replica.ReplicaManager;

        public virtual void Serialize(BitStream bs, SerializeContext ctx) { }
        public virtual void Deserialize(BitStream bs) { }

        /// Do NOT manually overwrite this. RpcPatcher will generate this dispatch table automatically.
        public virtual void DispatchRpc(byte methodIdx, BitStream bs) {
            Debug.LogError("Pure virtual RPC called, RpcPatcher needs to run");
        }
    }
}
