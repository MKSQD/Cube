
using UnityEngine;

namespace Cube.Replication {
    public interface IReplicaManager {
        Replica GetReplica(ReplicaId id);

        void Reset();
    }
}
