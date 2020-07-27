using Cube.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    public sealed class ClientReplicaManager : IClientReplicaManager {
#if UNITY_EDITOR
        public static List<ClientReplicaManager> all = new List<ClientReplicaManager>();
#endif

        ICubeClient _client;

        NetworkScene _networkScene;

        NetworkPrefabLookup _networkPrefabLookup;
        Dictionary<byte, SceneReplicaWrapper> _sceneReplicaLookup;

        float _nextUpdateTime;

        public ClientReplicaManager(ICubeClient client, NetworkPrefabLookup networkPrefabLookup) {
            Assert.IsNotNull(networkPrefabLookup);

            _networkPrefabLookup = networkPrefabLookup;

            _client = client;

            _client.reactor.AddHandler((byte)MessageId.ReplicaUpdate, OnReplicaUpdate);
            _client.reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);
            _client.reactor.AddHandler((byte)MessageId.ReplicaDestroy, OnReplicaDestroy);

            _networkScene = new NetworkScene();
            _sceneReplicaLookup = new Dictionary<byte, SceneReplicaWrapper>();

            SceneManager.sceneLoaded += OnSceneLoaded;

#if UNITY_EDITOR
            all.Add(this);
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

                replica.client = _client;
                replica.ReplicaId = ReplicaId.CreateFromExisting(replica.sceneIdx);
                _networkScene.AddReplica(replica);
            }
        }

        public void Reset() {
            _networkScene.DestroyAll();
        }

        public void RemoveReplica(Replica replica) {
            _networkScene.RemoveReplica(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            return _networkScene.GetReplicaById(id);
        }

        public void Update() {
            if (Time.time < _nextUpdateTime)
                return;

            _nextUpdateTime = Time.time + 1;

            var removeTime = Time.time - Constants.clientReplicaInactiveDestructionTimeSec;

            var replicas = _networkScene.replicas;
            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (replica == null || replica.isSceneReplica)
                    continue;

                if (replica.lastUpdateTime <= removeTime) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    UnityEngine.Object.Destroy(replica.gameObject);
                }
            }
        }

        void OnReplicaUpdate(BitStream bs) {
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

                replica = ConstructReplica(prefabIdx, replicaId);
                if (replica == null)
                    return; // Construction failed

                _networkScene.AddReplica(replica);
            }

            var isOwner = bs.ReadBool();
            replica.ClientUpdateOwnership(isOwner);

            // Hack: 
            // In the editor client and service scene Replica is the same instance. So we don't do
            // any ReplicaBehaviour replication
#if UNITY_EDITOR
            if (isSceneReplica)
                return;
#endif

            try {
                replica.Deserialize(bs);
            }
            catch (Exception e) {
                Debug.LogError("Exception while deserializing Replica " + replica + ":");
                Debug.LogException(e);
            }

            replica.lastUpdateTime = Time.time;
        }

        Replica ConstructReplica(ushort prefabIdx, ReplicaId replicaId) {
            if (!_networkPrefabLookup.TryGetClientPrefabForIndex(prefabIdx, out GameObject prefab)) {
                Debug.LogWarning("Prefab for index " + prefabIdx + " not found!");
                return null;
            }

            var newInstance = UnityEngine.Object.Instantiate(prefab, _client.world.transform);

            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null) {
                Debug.LogError("Replica component missing on " + prefab);
                return null;
            }

            newReplica.client = _client;
            newReplica.ReplicaId = replicaId;

            return newReplica;
        }

        void OnReplicaRpc(BitStream bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG
                Debug.LogError("Replica with id " + replicaId + " missing on client");
#endif
                return;
            }

            replica.CallRpcClient(bs, this);
        }

        void OnReplicaDestroy(BitStream bs) {
            while (!bs.IsExhausted) {
                var replicaId = bs.ReadReplicaId();
                var absOffset = bs.ReadUShort();

                var replica = _networkScene.GetReplicaById(replicaId);
                if (replica != null) {
                    replica.DeserializeDestruction(bs);
                    UnityEngine.Object.Destroy(replica.gameObject);
                }

                bs.Position = absOffset;
                bs.AlignReadToByteBoundary();
            }
        }
    }
}
