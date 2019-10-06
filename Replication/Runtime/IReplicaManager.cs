
using UnityEngine;

namespace Cube.Replication {
    public interface IReplicaManager {
        Replica GetReplicaById(ReplicaId id);

        void Reset();
    }
}
