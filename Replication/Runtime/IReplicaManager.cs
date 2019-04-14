
using UnityEngine;

namespace Cube.Replication {
    public interface IReplicaManager {
        Transform instantiateTransform {
            get;
        }

        Replica GetReplicaById(ReplicaId id);

        void DestroyAllReplicas();
    }
}
