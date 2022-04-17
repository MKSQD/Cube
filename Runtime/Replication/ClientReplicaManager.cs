using System;
using System.Collections.Generic;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public sealed class ClientReplicaManager : IClientReplicaManager {
#if UNITY_EDITOR
        public static List<ClientReplicaManager> All = new List<ClientReplicaManager>();
#endif

        readonly ICubeClient _client;
        readonly Transform _instantiateTransform;

        readonly NetworkScene _networkScene;
        readonly NetworkPrefabLookup _networkPrefabLookup;

        public ClientReplicaManager(ICubeClient client, Transform instantiateTransform) {
            Assert.IsNotNull(client);
            Assert.IsNotNull(instantiateTransform);

            _networkPrefabLookup = NetworkPrefabLookup.Instance;
            _instantiateTransform = instantiateTransform;
            _client = client;

            client.Reactor.AddHandler((byte)MessageId.ReplicaUpdate, OnReplicaUpdate);
            client.Reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);
            client.Reactor.AddHandler((byte)MessageId.ReplicaDestroy, OnReplicaDestroy);

            _networkScene = new NetworkScene();

            SceneManager.sceneLoaded += (scene, mode) => OnSceneLoaded(scene);

#if UNITY_EDITOR
            All.Add(this);
#endif
        }

        void OnSceneLoaded(Scene scene) {
            var sceneReplicas = ReplicaUtils.GatherSceneReplicas(scene);
            foreach (var replica in sceneReplicas) {
                replica.client = _client;
            }
        }

        public void ProcessSceneReplicasInScene(Scene scene) {
            Assert.IsTrue(scene.IsValid());

            var sceneReplicas = ReplicaUtils.GatherSceneReplicas(scene);
            foreach (var replica in sceneReplicas) {
                replica.Id = ReplicaId.CreateFromExisting(replica.sceneIdx);
                _networkScene.AddReplica(replica);
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
                if (replica == null || replica.isSceneReplica)
                    continue;

                var replicaTimeout = replica.lastUpdateTime < Time.time - replica.Settings.DesiredUpdateRate * 30;
                var canDestroy = !replica.IsOwner; // We don't ever destroy owned Replicas
                if (replicaTimeout && canDestroy) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }

        HashSet<ReplicaId> _replicasInConstruction = new HashSet<ReplicaId>();
        void OnReplicaUpdate(BitReader bs) {
            var prefabIdx = ushort.MaxValue;

            var isSceneReplica = bs.ReadBool();
            if (!isSceneReplica) {
                prefabIdx = bs.ReadUShort();
            }

            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
                if (isSceneReplica)
                    return; // Don't construct scene Replicas

                if (_replicasInConstruction.Contains(replicaId))
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
            if (!_networkPrefabLookup.TryGetClientPrefabForIndex(prefabIdx, out prefab))
                throw new Exception($"Prefab for index {prefabIdx} not found!");

            _replicasInConstruction.Add(replicaId);

            var newGameObject = GameObject.Instantiate(prefab, _instantiateTransform);

            var replica = newGameObject.GetComponent<Replica>();
            if (replica == null)
                throw new Exception("Replica component missing on " + prefab);

            replica.client = _client;
            replica.Id = replicaId;

            _replicasInConstruction.Remove(replicaId);
            _networkScene.AddReplica(replica);

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
