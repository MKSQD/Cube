using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Replication {
#if SERVER
    public interface IServerReplicaManager : IReplicaManager {
        IReplicaPriorityManager priorityManager {
            get;
        }

        List<ReplicaView> replicaViews {
            get;
        }

        void Update();
        
        GameObject InstantiateReplica(GameObject prefab);
        GameObject InstantiateReplica(GameObject prefab, Vector3 position);
        GameObject InstantiateReplica(GameObject prefab, Vector3 position, Quaternion rotation);

        /// <summary>
        /// Remove the Replica instantly from the manager, without notifying the clients.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        void RemoveReplica(Replica replica);

        /// <summary>
        /// Remove the Replica instantly from the manager, destroys the gameobject and send a destroy message to the clients on the next update.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        void DestroyReplica(Replica replica);

        ReplicaView GetReplicaView(Connection connection);
        void AddReplicaView(ReplicaView view);
        void RemoveReplicaView(Connection connection);

        ushort AllocateLocalReplicaId();
        void FreeLocalReplicaId(ushort localId);
    }
#endif
}

