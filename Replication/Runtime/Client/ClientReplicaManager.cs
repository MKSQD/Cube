using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
#if CLIENT
    public sealed class ClientReplicaManager : IClientReplicaManager {
        IUnityClient _client;

        NetworkScene _networkScene;

        NetworkPrefabLookup _networkPrefabLookup;
        Dictionary<byte, SceneReplicaWrapper> _sceneReplicaLookup;

        Transform _clientTransform;
        public Transform instantiateTransform {
            get { return _clientTransform; }
        }

        float _nextUpdateTime;

        public ClientReplicaManager(IUnityClient client, NetworkPrefabLookup networkPrefabLookup, Transform clientTransform) {
            Assert.IsNotNull(networkPrefabLookup);

            _clientTransform = clientTransform;
            _networkPrefabLookup = networkPrefabLookup;

            _client = client;

            _client.reactor.AddHandler((byte)MessageId.ReplicaFullUpdate, new ClientMessageHandler(OnReplicaFullUpdate));
            _client.reactor.AddHandler((byte)MessageId.ReplicaPartialUpdate, new ClientMessageHandler(OnReplicaPartialUpdate));
            _client.reactor.AddHandler((byte)MessageId.ReplicaRpc, new ClientMessageHandler(OnReplicaRpc));
            _client.reactor.AddHandler((byte)MessageId.ReplicaDestroy, new ClientMessageHandler(OnReplicaDestroy));

            _networkScene = new NetworkScene();
            _sceneReplicaLookup = new Dictionary<byte, SceneReplicaWrapper>();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                var replica = go.GetComponent<Replica>();
                if (replica == null)
                    continue;

                sceneReplicas.Add(replica);
            }

            sceneReplicas.Sort((r1, r2) => r1.sceneIdx - r2.sceneIdx);

            foreach (var replica in sceneReplicas) {
                replica.client = _client;
                replica.id = ReplicaId.CreateFromExisting(replica.sceneIdx);
                _networkScene.AddReplica(replica);
            }
        }

        public void Reset() {
            for (int i = 0; i < _networkScene.replicas.Count; ++i) {
                Object.Destroy(_networkScene.replicas[i].gameObject);
            }
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
                if (replica.isSceneReplica)
                    continue;

                if (replica.lastUpdateTime <= removeTime) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    Object.Destroy(replica.gameObject);
                }
            }
        }

        void OnReplicaFullUpdate(BitStream bs) {
            var isOwner = bs.ReadBool();
            var isSceneReplica = bs.ReadBool();
            ushort prefabIdx = ushort.MaxValue;
            if (!isSceneReplica) {
                prefabIdx = bs.ReadUShort();
            }
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
                if (isSceneReplica)
                    return;

                replica = ConstructReplica(prefabIdx, replicaId);
                if (replica == null)
                    return;

                _networkScene.AddReplica(replica);
            }

            replica.isOwner = isOwner;

            foreach (var component in replica.replicaBehaviours) {
                component.Deserialize(bs, ReplicaSerializationMode.Full);
            }

            replica.lastUpdateTime = Time.time;
        }

        Replica ConstructReplica(ushort prefabIdx, ReplicaId replicaId) {
            GameObject prefab;
            if (!_networkPrefabLookup.TryGetClientPrefabForIndex(prefabIdx, out prefab)) {
                Debug.LogWarning("Prefab for index " + prefabIdx + " not found!");
                return null;
            }

            var newInstance = Object.Instantiate(prefab, _clientTransform);

            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null) {
                Debug.LogWarning("Replica component missing on " + prefab);
                return null;
            }

            newReplica.client = _client;
            newReplica.id = replicaId;
            return newReplica;
        }
        
        void OnReplicaPartialUpdate(BitStream bs) {
            var replicaId = bs.ReadReplicaId();
            var replica = _networkScene.GetReplicaById(replicaId);

            //This can happen if the Replica is not fully constructed
            if (replica == null)
                return;

            foreach (var component in replica.replicaBehaviours) {
                component.Deserialize(bs, ReplicaSerializationMode.Partial);
            }

            replica.lastUpdateTime = Time.time;
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
            var count = bs.ReadByte();

            for (int i = 0; i < count; ++i) {
                var replicaId = bs.ReadReplicaId();

                var replica = _networkScene.GetReplicaById(replicaId);
                if (replica == null)
                    continue;

                replica.gameObject.SendMessage("OnReplicaDestroy", SendMessageOptions.DontRequireReceiver);
                Object.Destroy(replica.gameObject);
            }
        }
    }
#endif
}
