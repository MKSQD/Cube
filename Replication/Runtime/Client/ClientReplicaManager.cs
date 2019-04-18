using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
#if CLIENT
    public sealed class ClientReplicaManager : IClientReplicaManager {
        IClientReactor _reactor;
        public IClientReactor reactor {
            get { return _reactor; }
        }

        NetworkScene _networkScene;

        NetworkPrefabLookup _networkPrefabLookup;
        Dictionary<byte, SceneReplicaWrapper> _sceneReplicaLookup;

        Transform _clientTransform;
        public Transform instantiateTransform {
            get { return _clientTransform; }
        }

        float _nextUpdateTime;

        public ClientReplicaManager(IClientReactor reactor, NetworkPrefabLookup networkPrefabLookup, Transform clientTransform) {
            Assert.IsNotNull(networkPrefabLookup);

            _clientTransform = clientTransform;
            _networkPrefabLookup = networkPrefabLookup;

            _reactor = reactor;
            _reactor.AddHandler((byte)MessageId.ReplicaFullUpdate, new ClientMessageHandler(OnReplicaFullUpdate));
            _reactor.AddHandler((byte)MessageId.ReplicaPartialUpdate, new ClientMessageHandler(OnReplicaPartialUpdate));
            _reactor.AddHandler((byte)MessageId.ReplicaRpc, new ClientMessageHandler(OnReplicaRpc));
            _reactor.AddHandler((byte)MessageId.ReplicaDestroy, new ClientMessageHandler(OnReplicaDestroy));

            _networkScene = new NetworkScene();
            _sceneReplicaLookup = new Dictionary<byte, SceneReplicaWrapper>();
        }

        public void DestroyAllReplicas() {
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
                if (replica.lastUpdateTime <= removeTime) {
                    // Note we modify the replicas variable implicitly here -> the Replica deletes itself
                    Object.Destroy(replica.gameObject);
                }
            }
        }

        void OnReplicaFullUpdate(BitStream bs) {
            var isSceneReplica = bs.ReadBool();
            var isOwner = bs.ReadBool();

            var sceneIdx = byte.MaxValue;
            if (isSceneReplica) {
                sceneIdx = bs.ReadByte();
            }

            var prefabIdx = bs.ReadUShort();
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
                replica = !isSceneReplica ? ConstructReplica(prefabIdx, replicaId) : ConstructSceneReplica(sceneIdx, prefabIdx, replicaId);
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

            return ConstructReplicaImpl(prefab, replicaId);
        }

        Replica ConstructReplicaImpl(GameObject prefab, ReplicaId replicaId) {
            var newInstance = Object.Instantiate(prefab, _clientTransform);

            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null) {
                Debug.LogWarning("Replica component missing on " + prefab);
                return null;
            }

            newReplica.id = replicaId;
            return newReplica;
        }

        Replica ConstructSceneReplica(byte sceneIdx, ushort prefabIdx, ReplicaId replicaId) {
            SceneReplicaWrapper wrapper = null;

            //wrapper can be null because we never cleanup the dictionary
            if (!_sceneReplicaLookup.TryGetValue(sceneIdx, out wrapper) || wrapper == null) {
                //because => lazy == cool
                for (int i = 0; i < SceneManager.sceneCount; i++) {
                    var scene = SceneManager.GetSceneAt(i);

                    wrapper = SceneReplicaUtil.GetSceneReplicaWrapper(scene);
                    if (wrapper == null || wrapper.sceneId != sceneIdx)
                        continue;

                    _sceneReplicaLookup[sceneIdx] = wrapper;
                }
            }

            //This can happen if the scene is not loaded on client side. Ignore and try again next time
            if (wrapper == null)
                return null;

            var blueprint = wrapper.transform.GetChild(prefabIdx).gameObject;   //prefabIdx == childIdx;
            var newInstance = ConstructReplicaImpl(blueprint, replicaId);
            newInstance.gameObject.SetActive(true);
            return newInstance;
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
