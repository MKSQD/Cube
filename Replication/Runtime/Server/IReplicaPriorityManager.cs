

namespace Cube.Replication {
    public struct PriorityResult {
        public float relevance;
        public float final;
    }

    public interface IReplicaPriorityManager {
#if SERVER
        float minPriorityForSending {
            get;
        }

        PriorityResult GetPriority(Replica replica, ReplicaView view);
#endif
    }
}
