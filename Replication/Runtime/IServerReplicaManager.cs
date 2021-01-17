using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Cube.Replication {
    public interface IServerReplicaManager : IReplicaManager {
        List<ReplicaView> ReplicaViews {
            get;
        }

        void Update();
        
        GameObject InstantiateReplica(GameObject prefab);
        GameObject InstantiateReplica(GameObject prefab, Vector3 position);
        GameObject InstantiateReplica(GameObject prefab, Vector3 position, Quaternion rotation);

        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key);
        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position);
        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position, Quaternion rotation);

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
        void ForceReplicaViewRefresh(ReplicaView view);

        ushort AllocateLocalReplicaId();
        void FreeLocalReplicaId(ReplicaId id);
    }
}

