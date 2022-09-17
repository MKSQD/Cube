﻿using System.Collections.ObjectModel;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public interface IServerReplicaManager : IReplicaManager {
        ReadOnlyCollection<Replica> Replicas { get; }

        void ProcessSceneReplicasInScene(Scene scene);

        void Tick();
        void Update();


        GameObject InstantiateReplica(GameObject prefab);
        GameObject InstantiateReplica(GameObject prefab, Vector3 position);
        GameObject InstantiateReplica(GameObject prefab, Vector3 position, Quaternion rotation);


        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference key);
        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference key, Vector3 position);
        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference key, Vector3 position, Quaternion rotation);

        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key);
        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position);
        AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position, Quaternion rotation);

        void AddReplica(GameObject newInstance);
        void AddReplica(Replica newReplica);

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
    }
}

