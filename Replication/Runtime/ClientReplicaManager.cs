using Cube.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    public sealed class ClientReplicaManager : IClientReplicaManager {
#if UNITY_EDITOR
        public static List<ClientReplicaManager> All = new List<ClientReplicaManager>();
#endif

        ICubeClient client;

        NetworkScene networkScene;
        NetworkPrefabLookup networkPrefabLookup;

        float nextUpdateTime;

        public ClientReplicaManager(ICubeClient client, NetworkPrefabLookup networkPrefabLookup) {
            Assert.IsNotNull(networkPrefabLookup);

            this.networkPrefabLookup = networkPrefabLookup;
            this.client = client;

            client.Reactor.AddHandler((byte)MessageId.ReplicaUpdate, OnReplicaUpdate);
            client.Reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);
            client.Reactor.AddHandler((byte)MessageId.ReplicaDestroy, OnReplicaDestroy);

            networkScene = new NetworkScene();

            SceneManager.sceneLoaded += (scene, mode) => ProcessSceneReplicasInSceneInternal(scene);

#if UNITY_EDITOR
            All.Add(this);
#endif
        }

        void ProcessSceneReplicasInSceneInternal(Scene scene) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    if (!replica.isSceneReplica)
                        continue;

                    replica.client = client;
                }
            }
        }

        public void ProcessSceneReplicasInScene(Scene scene) {
            Debug.Log("[Client] <b>Scene loaded</b>");

            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    if (!replica.isSceneReplica)
                        continue;

                    replica.Id = ReplicaId.CreateFromExisting(replica.sceneIdx);
                    networkScene.AddReplica(replica);
                }
            }
        }

        public void Reset() {
            networkScene.DestroyAll();
        }

        public void RemoveReplica(Replica replica) {
            networkScene.RemoveReplica(replica);
        }

        public Replica GetReplica(ReplicaId id) {
            return networkScene.GetReplicaById(id);
        }

        public void Update() {
            if (Time.time < nextUpdateTime)
                return;

            nextUpdateTime = Time.time + 1;

            var replicas = networkScene.Replicas;
            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (replica == null || replica.isSceneReplica)
                    continue;

                var replicaTimeout = replica.lastUpdateTime < Time.time - replica.settings.DesiredUpdateRate * 30;
                var canDestroy = !replica.IsOwner; // We don't ever destroy owned Replicas
                if (replicaTimeout && canDestroy) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }

        HashSet<ReplicaId> replicasInConstruction = new HashSet<ReplicaId>();
        void OnReplicaUpdate(BitStream bs) {
            var prefabIdx = ushort.MaxValue;

            var isSceneReplica = bs.ReadBool();
            if (!isSceneReplica) {
                prefabIdx = bs.ReadUShort();
            }

            var replicaId = bs.ReadReplicaId();

            var replica = networkScene.GetReplicaById(replicaId);
            if (replica == null) {
                if (isSceneReplica)
                    return; // Don't construct scene Replicas

                if (replicasInConstruction.Contains(replicaId))
                    return;

                replica = ConstructReplica(prefabIdx, replicaId);
            }

            var isOwner = bs.ReadBool();
            if (isOwner != replica.IsOwner) {
                replica.ClientUpdateOwnership(isOwner);
            }

            // Hack: 
            // In the editor client and service scene Replica is the same instance. So we don't do
            // any replication
#if UNITY_EDITOR
            if (isSceneReplica)
                return;
#endif

            try {
                replica.Deserialize(bs);
            } catch (Exception e) {
                Debug.LogException(e, replica.gameObject);
            }

            replica.lastUpdateTime = Time.time;
        }

        Replica ConstructReplica(ushort prefabIdx, ReplicaId replicaId) {
            GameObject prefab;
            if (!networkPrefabLookup.TryGetClientPrefabForIndex(prefabIdx, out prefab))
                throw new Exception($"Prefab for index {prefabIdx} not found!");

            replicasInConstruction.Add(replicaId);

            var newGameObject = GameObject.Instantiate(prefab, client.World.transform);

            var replica = newGameObject.GetComponent<Replica>();
            if (replica == null)
                throw new Exception("Replica component missing on " + prefab);

            replica.client = client;
            replica.Id = replicaId;

            replicasInConstruction.Remove(replicaId);
            networkScene.AddReplica(replica);

            return replica;
        }

        void OnReplicaRpc(BitStream bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG
                Debug.LogError("Replica with id " + replicaId + " missing on client");
#endif
                return;
            }

            replica.CallRpcClient(bs);
        }

        void OnReplicaDestroy(BitStream bs) {
            while (!bs.IsExhausted) {
                var replicaId = bs.ReadReplicaId();
                var replica = networkScene.GetReplicaById(replicaId);
                if (replica != null) {
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }
    }
}
