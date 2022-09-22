
using UnityEngine;

namespace Cube.Replication {
    public interface IReplicaManager {
        /// <summary>
        /// Remove the Replica instantly from the manager, without sending any packets.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        void RemoveReplica(Replica replica);

        Replica GetReplica(ReplicaId id);

        void Reset();
        void Shutdown();
    }
}
