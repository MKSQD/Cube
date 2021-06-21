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

            client.reactor.AddHandler((byte)MessageId.ReplicaUpdate, OnReplicaUpdate);
            client.reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);
            client.reactor.AddHandler((byte)MessageId.ReplicaDestroy, OnReplicaDestroy);

            networkScene = new NetworkScene();

            SceneManager.sceneLoaded += OnSceneLoaded;

#if UNITY_EDITOR
            All.Add(this);
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    sceneReplicas.Add(replica);
                }
            }

            sceneReplicas.Sort((r1, r2) => r1.sceneIdx - r2.sceneIdx);

            foreach (var replica in sceneReplicas) {
                if (replica.sceneIdx == 0) {
                    Debug.LogWarning("[Client] scene Replica had no valid sceneIdx; edit and save the scene to generate valid ones", replica.gameObject);
                    continue;
                }

#if UNITY_EDITOR
                foreach (var existingReplica in networkScene.Replicas) {
                    if (replica.sceneIdx == existingReplica.sceneIdx) {
                        Debug.LogWarning("Replicas with the same sceneIdx found!");

                        networkScene.RemoveReplica(existingReplica);

                        break;
                    }
                }
#endif

                replica.client = client;
                replica.Id = ReplicaId.CreateFromExisting(replica.sceneIdx);
                networkScene.AddReplica(replica);
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

                if (replica.lastUpdateTime < Time.time - replica.settings.DesiredUpdateRate * 30) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }

        HashSet<ReplicaId> replicasInConstruction = new HashSet<ReplicaId>();
        void OnReplicaUpdate(BitStream bs) {
            var prePosition = bs.Position;

            var prefabIdx = ushort.MaxValue;

            var isSceneReplica = bs.ReadBool();
            if (!isSceneReplica) {
                prefabIdx = bs.ReadUShort();
            }

            var replicaId = bs.ReadReplicaId();
            if (replicasInConstruction.Contains(replicaId))
                return;

            var replica = networkScene.GetReplicaById(replicaId);
            if (replica == null) {
                if (isSceneReplica)
                    return; // Don't construct scene Replicas

                var bsCopy = BitStream.CopyExistingBuffer(bs.Data, prePosition, bs.LengthInBits);
                ConstructReplica(prefabIdx, replicaId, bsCopy);
                return;
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

        void ConstructReplica(ushort prefabIdx, ReplicaId replicaId, BitStream bsCopy) {
            AssetReferenceGameObject prefab;
            if (!networkPrefabLookup.TryGetClientPrefabForIndex(prefabIdx, out prefab))
                throw new Exception($"Prefab for index {prefabIdx} not found!");

            replicasInConstruction.Add(replicaId);

            var result = prefab.InstantiateAsync(client.world.transform);
            result.Completed += ctx => {
                var newReplica = ctx.Result.GetComponent<Replica>();
                if (newReplica == null) {
                    Debug.LogError("Replica component missing on " + prefab);
                    return;
                }

                newReplica.client = client;
                newReplica.Id = replicaId;

                replicasInConstruction.Remove(replicaId);
                networkScene.AddReplica(newReplica);

                // Make sure the Replica has all it's state before Start(), etc. can run
                // Not pretty but not having this leads to a Replica being in an uninitialized state
                // for a potentially very long time while we had this info. All this mess is the result
                // of Addressables not having a sync API which should be used for Replicas.
                OnReplicaUpdate(bsCopy);
            };
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
            var replicaId = bs.ReadReplicaId();

            var replica = networkScene.GetReplicaById(replicaId);
            if (replica != null) {
                replica.DeserializeDestruction(bs);
                UnityEngine.Object.Destroy(replica.gameObject);
            }
        }
    }
}
