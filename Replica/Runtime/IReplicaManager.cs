
using UnityEngine;

namespace Cube.Networking.Replicas {
    public interface IReplicaManager {
        Transform instantiateTransform {
            get;
        }

        Replica GetReplicaById(ReplicaId id);
    }
}
