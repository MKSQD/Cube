using Cube.Transport;
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
        public Replica Replica;

        public static Connection rpcConnection = Connection.Invalid;

        public ICubeServer server => Replica.server;
        public ICubeClient client => Replica.client;
        public IReplicaManager ReplicaManager => Replica.ReplicaManager;

        public bool isServer => Replica.isServer;
        public bool isClient => Replica.isClient;
        public bool isOwner => Replica.IsOwner;

        public virtual void Serialize(BitStream bs, SerializeContext ctx) { }
        public virtual void Deserialize(BitStream bs) { }

        public virtual void SerializeDestruction(BitStream bs, SerializeContext ctx) { }
        public virtual void DeserializeDestruction(BitStream bs) { }

        /// Do NOT manually overwrite this. RpcPatcher will generate this dispatch table automatically.
        public virtual void DispatchRpc(byte methodIdx, BitStream bs) {
            Debug.LogError("Pure virtual RPC called, RpcPatcher needs to run");
        }
    }
}
