using System;
using System.Collections.Generic;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public sealed class ClientReplicaManager : IReplicaManager {
#if UNITY_EDITOR
        public static List<ClientReplicaManager> All = new();
#endif

        readonly ICubeClient _client;
        readonly Transform _instantiateTransform;

        readonly NetworkScene _networkScene;
        readonly NetworkPrefabs _networkPrefabLookup;

        public ClientReplicaManager(ICubeClient client, NetworkPrefabs networkPrefabLookup, Transform instantiateTransform) {
            Assert.IsNotNull(client);
            Assert.IsNotNull(instantiateTransform);

            _networkPrefabLookup = networkPrefabLookup;
            _instantiateTransform = instantiateTransform;
            _client = client;

            client.Reactor.AddPacketHandler((byte)MessageId.ReplicaUpdate, OnReplicaUpdate);
            client.Reactor.AddPacketHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);
            client.Reactor.AddPacketHandler((byte)MessageId.ReplicaDestroy, OnReplicaDestroy);

            _networkScene = new NetworkScene();

            SceneManager.sceneLoaded += OnSceneLoaded;

#if UNITY_EDITOR
            All.Add(this);
#endif
        }

        public void Shutdown() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode _) {
            var sceneReplicas = ReplicaUtils.GatherSceneReplicas(scene);
            foreach (var replica in sceneReplicas) {
                replica.client = _client;
            }
        }

        public void ProcessSceneReplicasInScene(Scene scene) {
            Assert.IsTrue(scene.IsValid());

            var sceneReplicas = ReplicaUtils.GatherSceneReplicas(scene);
            foreach (var replica in sceneReplicas) {
                AddReplica(replica);
            }
        }

        public void Reset() => _networkScene.DestroyAll();

        public void RemoveReplica(Replica replica) => _networkScene.RemoveReplica(replica);

        public Replica GetReplica(ReplicaId id) {
            return _networkScene.GetReplicaById(id);
        }

        public void Tick() {
            var replicas = _networkScene.Replicas;
            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (replica == null || replica.HasStaticId)
                    continue;

                var replicaTimeout = replica.lastUpdateTime < Time.time - replica.Settings.DesiredUpdateRate * 30;
                var canDestroy = !replica.IsOwner; // We don't ever destroy owned Replicas
                if (replicaTimeout && canDestroy) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }

        public void AddReplica(Replica newReplica) {
            newReplica.client = _client;

            if (newReplica.HasStaticId) {
                if (newReplica.Id == ReplicaId.Invalid) {
                    newReplica.Id = ReplicaId.CreateFromExisting(newReplica.StaticId);
                }
            } else {
                Assert.IsTrue(newReplica.Id != ReplicaId.Invalid);
            }

            _networkScene.AddReplica(newReplica);
        }

        void OnReplicaUpdate(BitReader bs) {
            var replicaId = bs.ReadReplicaId();
            var isSceneReplica = bs.ReadBool();

            ushort prefabHash = 0;
            if (!isSceneReplica) {
                prefabHash = bs.ReadUShort();
            }
            var isOwner = bs.ReadBool();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
                if (isSceneReplica)
                    return; // Don't construct scene Replicas


                replica = ConstructReplica(prefabHash, replicaId);
                AddReplica(replica);
            }

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

        Replica ConstructReplica(ushort prefabHash, ReplicaId replicaId) {
            var prefabIdx = _networkPrefabLookup.GetIndexForHash(prefabHash);
            var prefab = _networkPrefabLookup.GetClientPrefabForIndex(prefabIdx);
            var newGameObject = UnityEngine.Object.Instantiate(prefab, new Vector3(100000, 100000, 100000), Quaternion.identity, _instantiateTransform);

            var replica = newGameObject.GetComponent<Replica>();
            if (replica == null)
                throw new Exception("Replica component missing on " + prefab);

            replica.Id = replicaId;
            replica.client = _client;
            return replica;
        }

        void OnReplicaRpc(BitReader bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG
                Debug.LogError("Replica with id " + replicaId + " missing on client");
#endif
                return;
            }

            replica.CallRpcClient(bs);
        }

        void OnReplicaDestroy(BitReader bs) {
            while (!bs.WouldReadPastEnd(1)) { // #todo 1?
                var replicaId = bs.ReadReplicaId();
                var replica = _networkScene.GetReplicaById(replicaId);
                if (replica != null) {
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }
    }
}
