﻿using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using UnityEngine.SceneManagement;
using Cube.Transport;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    [Serializable]
    public class ServerReplicaManagerSettings {
        [Range(0, 1500)]
        public int maxBytesPerConnectionPerUpdate = 1400;
        [Range(10, 1000)]
        public float replicaUpdateRateMS = 33; // 30 times per second
    }

#if SERVER
    public sealed class ServerReplicaManager : IServerReplicaManager {
        public class Statistic {
            public struct PerReplicaTypeInfo {
                public int numInstances;
                public int totalBytes;
            }

            public class PerReplicaViewInfo {
                public Dictionary<int, PerReplicaTypeInfo> bytesPerPrefabIdx = new Dictionary<int, PerReplicaTypeInfo>();
            }

            public struct ReplicaViewInfoPair {
                public ReplicaView view;
                public PerReplicaViewInfo info;
            }

            public List<ReplicaViewInfoPair> viewInfos = new List<ReplicaViewInfoPair>();
        }

        IServerReactor _reactor;
        public IServerReactor reactor {
            get { return _reactor; }
        }

        NetworkScene _networkScene;

        Transform _serverTransform;
        public Transform instantiateTransform {
            get { return _serverTransform; }
        }

        IReplicaPriorityManager _priorityManager;
        public IReplicaPriorityManager priorityManager {
            get { return _priorityManager; }
        }

        [SerializeField]
        List<ReplicaView> _replicaViews = new List<ReplicaView>();
        public List<ReplicaView> replicaViews {
            get { return _replicaViews; }
        }

        ServerReplicaManagerSettings _settings;

        float _nextUpdateTime;

        ushort _nextLocalReplicaId = 1; // 1, so 0 can be the Invalid value (structs are zeroed to 0)
        Queue<ushort> _freeReplicaIds = new Queue<ushort>();

        float _nextReplicaIdRecycleTime = 0;
        Queue<ushort> _replicaIdRecycleQueue = new Queue<ushort>();

        Dictionary<ReplicaId, Replica> _constructingReplicas = new Dictionary<ReplicaId, Replica>();
        List<ReplicaId> _destroyedReplicas = new List<ReplicaId>();

#if UNITY_EDITOR
        Statistic _statistic;
        public Statistic statistic {
            get { return _statistic; }
        }
#endif

        public ServerReplicaManager(IServerReactor reactor, Transform serverTransform, IReplicaPriorityManager priorityManager, ServerReplicaManagerSettings settings) {
            Assert.IsNotNull(reactor);
            Assert.IsNotNull(serverTransform);
            Assert.IsNotNull(priorityManager);
            Assert.IsNotNull(settings);

            _serverTransform = serverTransform;

            _networkScene = new NetworkScene();
            _priorityManager = priorityManager;

            _reactor = reactor;
            _reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);

            _settings = settings;

            SceneManager.sceneLoaded += InstantiateSceneReplicas;
        }

        void InstantiateSceneReplicas(Scene scene, LoadSceneMode mode) {
            var sceneReplicas = SceneReplicaUtil.FindSceneReplicasInScene(scene);
            foreach (var sceneReplica in sceneReplicas) {
                sceneReplica.transform.parent = _serverTransform;
                sceneReplica.id = ReplicaId.Create(this);

                _constructingReplicas.Add(sceneReplica.id, sceneReplica);
            }
        }

        public GameObject InstantiateReplica(GameObject prefab) {
            return InstantiateReplica(prefab, Vector3.zero, Quaternion.identity);
        }

        public GameObject InstantiateReplica(GameObject prefab, Vector3 position) {
            return InstantiateReplica(prefab, position, Quaternion.identity);
        }

        public GameObject InstantiateReplica(GameObject prefab, Vector3 position, Quaternion rotation) {
            Assert.IsNotNull(prefab);

            var replica = InstantiateReplicaImpl(prefab, position, rotation);
            return replica != null ? replica.gameObject : null;
        }

        Replica InstantiateReplicaImpl(GameObject prefab, Vector3 position, Quaternion rotation) {
            var newInstance = UnityEngine.Object.Instantiate(prefab, position, rotation, _serverTransform);

            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null) {
                Debug.LogError("prefab is missing Replica Component: " + prefab, prefab);
                return null;
            }

            newReplica.id = ReplicaId.Create(this);

            //Delay sending to client because we should wait for one frame until Start is called.
            _constructingReplicas.Add(newReplica.id, newReplica);

            return newReplica;
        }

        /// <summary>
        /// Remove the Replica instantly from the manager, without notifying the clients.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        public void RemoveReplica(Replica replica) {
            //Replica already removed
            if (replica.id == ReplicaId.Invalid)
                return;

            _constructingReplicas.Remove(replica.id);
            _networkScene.RemoveReplica(replica);

            if (replica.id != ReplicaId.Invalid) {
                FreeLocalReplicaId(replica.id.data);
            }

            replica.id = ReplicaId.Invalid;
        }

        /// <summary>
        /// Removes the Replica instantly from this manager, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        public void DestroyReplica(Replica replica) {
            if (replica.id == ReplicaId.Invalid)
                return; //Replica already destroyed

            //we dont need to send a destroy message if the replica is not yet fully constructed on the server
            bool alreadyConstructed = !_constructingReplicas.Remove(replica.id);
            if (alreadyConstructed) {
                _destroyedReplicas.Add(replica.id);
            }
            _networkScene.RemoveReplica(replica);
            replica.id = ReplicaId.Invalid;
            UnityEngine.Object.Destroy(replica.gameObject);
        }

        public Replica GetReplicaById(ReplicaId id) {
            return _networkScene.GetReplicaById(id);
        }

        void RecycleReplicaIdsAfterDelay() {
            if (Time.time < _nextReplicaIdRecycleTime)
                return;

            if (_replicaIdRecycleQueue.Count > 0) {
                var id = _replicaIdRecycleQueue.Dequeue();
                _freeReplicaIds.Enqueue(id);
            }

            _nextReplicaIdRecycleTime = Time.time + Constants.serverReplicaIdRecycleTime;
        }

        public void Update() {
            RecycleReplicaIdsAfterDelay();

            if (Time.time < _nextUpdateTime)
                return;

            _nextUpdateTime = Time.time + _settings.replicaUpdateRateMS * 0.001f;
#if UNITY_EDITOR
            _statistic = new Statistic();
#endif

            UpdateReplicaViews();
            ClearReplicaBehavioursRpcsInNetworkScene();

            foreach (var replicaId in _destroyedReplicas) {
                FreeLocalReplicaId(replicaId.data);
            }
            _destroyedReplicas.Clear();

            foreach (var entry in _constructingReplicas) {
                _networkScene.AddReplica(entry.Value);
            }
            _constructingReplicas.Clear();
        }

        void UpdateReplicaViews() {
            for (int i = 0; i < _replicaViews.Count; ++i) {
                UpdateReplicaView(_replicaViews[i]);
            }

            // #TODO remove replicas from view.replicaUpdateInfo when the client removes them due to no updates/client leaves
        }

        void ClearReplicaBehavioursRpcsInNetworkScene() {
            foreach (var replica in _networkScene.replicas) {
                foreach (var component in replica.replicaBehaviours) {
                    component.rpcs.Clear();
                }
            }
        }

        void UpdateReplicaView(ReplicaView view) {
            SendDestroyedReplicasToReplicaView(view);

#if UNITY_EDITOR
            var perReplicaViewInfo = new Statistic.PerReplicaViewInfo();
            _statistic.viewInfos.Add(new Statistic.ReplicaViewInfoPair { view = view, info = perReplicaViewInfo });
#endif

            var replicas = SortReplicasByPriority(_networkScene.replicas, view);
            int bytesSent = 0;

            int numUpdatesSent = 0;
            int numRpcsSent = 0;
            foreach (var replica in replicas) {
                ReplicaView.UpdateInfo info;
                var firstUpdate = false;

                if (!view.replicaUpdateInfo.TryGetValue(replica, out info)) {
                    firstUpdate = true;
                    info = new ReplicaView.UpdateInfo();
                }

                var updateBitStream = _reactor.networkInterface.bitStreamPool.Create();

                var isFullUpdate = Time.time >= info.nextFullUpdateTime || firstUpdate;

                var messageId = !isFullUpdate ? (byte)MessageId.ReplicaPartialUpdate : (byte)MessageId.ReplicaFullUpdate;
                updateBitStream.Write(messageId);

                if (isFullUpdate) {
                    bool isSceneReplica = replica.sceneIdx != byte.MaxValue;
                    updateBitStream.Write(isSceneReplica);

                    if (isSceneReplica) {
                        updateBitStream.Write(replica.sceneIdx);
                    }

                    updateBitStream.Write(replica.prefabIdx);
                }

                updateBitStream.Write(replica.id);

                var serializationMode = isFullUpdate ? ReplicaSerializationMode.Full : ReplicaSerializationMode.Partial;
                foreach (var component in replica.replicaBehaviours) {
                    component.Serialize(updateBitStream, serializationMode, view);
                }

                _reactor.networkInterface.Send(updateBitStream, PacketPriority.Medium, PacketReliability.Unreliable, view.connection);

                ++numUpdatesSent;

                info.lastUpdateTime = Time.time;
                if (isFullUpdate) {
                    info.nextFullUpdateTime = Time.time + Constants.replicaFullUpdateRateMS * 0.001f;
                }

                view.replicaUpdateInfo[replica] = info;

                bytesSent += updateBitStream.Length;

#if UNITY_EDITOR
                Statistic.PerReplicaTypeInfo replicaTypeInfo;
                perReplicaViewInfo.bytesPerPrefabIdx.TryGetValue(replica.prefabIdx, out replicaTypeInfo);
                ++replicaTypeInfo.numInstances;
                replicaTypeInfo.totalBytes += updateBitStream.Length;

                perReplicaViewInfo.bytesPerPrefabIdx[replica.prefabIdx] = replicaTypeInfo;
#endif

                if (bytesSent >= _settings.maxBytesPerConnectionPerUpdate)
                    return;

                // rpcs
                foreach (var component in replica.replicaBehaviours) {
                    foreach (var rpc in component.rpcs) {
                        _reactor.networkInterface.Send(rpc, PacketPriority.Low, PacketReliability.Unreliable, view.connection);
                        ++numRpcsSent;

                        bytesSent += rpc.Length;
                        if (bytesSent >= _settings.maxBytesPerConnectionPerUpdate)
                            return;
                    }
                }
            }
        }

        struct PriorityReplicaPair {
            public float priority;
            public Replica replica;
        }
        List<Replica> _tmpReplicaList = new List<Replica>();
        List<PriorityReplicaPair> _tmpReplicaPriorities = new List<PriorityReplicaPair>();
        List<Replica> SortReplicasByPriority(ReadOnlyCollection<Replica> replicas, ReplicaView view) {
            // #optimize don't generate garbage please

            _tmpReplicaList.Clear();
            _tmpReplicaPriorities.Clear();

            var minPriority = priorityManager.minPriorityForSending;

            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (!replica.gameObject.activeInHierarchy)
                    continue;

                var priority = priorityManager.GetPriority(replica, view);
                if (priority.final < minPriority)
                    continue;

                var newPair = new PriorityReplicaPair {
                    priority = priority.final,
                    replica = replica
                };
                _tmpReplicaPriorities.Add(newPair);
            }

            _tmpReplicaPriorities.Sort((lhs, rhs) => (int)((rhs.priority - lhs.priority) * 100));

            for (int i = 0; i < _tmpReplicaPriorities.Count; ++i) {
                _tmpReplicaList.Add(_tmpReplicaPriorities[i].replica);
            }

            return _tmpReplicaList;
        }

        void SendDestroyedReplicasToReplicaView(ReplicaView view) {
            if (_destroyedReplicas.Count == 0)
                return;

            var destroyBs = _reactor.networkInterface.bitStreamPool.Create();
            destroyBs.Write((byte)MessageId.ReplicaDestroy);
            destroyBs.Write((byte)_destroyedReplicas.Count);

            foreach (var id in _destroyedReplicas) {
                destroyBs.Write(id);
            }

            _reactor.networkInterface.Send(destroyBs, PacketPriority.Medium, PacketReliability.Unreliable, view.connection);
        }

        public ReplicaView GetReplicaView(Connection connection) {
            foreach (var replicaView in _replicaViews) {
                if (replicaView.connection == connection)
                    return replicaView;
            }
            return null;
        }

        public void AddReplicaView(ReplicaView view) {
            if (_replicaViews.Contains(view))
                return;

            if (view.connection == Connection.Invalid)
                throw new ArgumentException("connection");

            _replicaViews.Add(view);
        }

        public void RemoveReplicaView(Connection connection) {
            for (int i = 0; i < _replicaViews.Count; ++i) {
                if (_replicaViews[i].connection == connection) {
                    _replicaViews.RemoveAt(i);
                    return;
                }
            }
        }

        public ushort AllocateLocalReplicaId() {
            if (_freeReplicaIds.Count > 0)
                return _freeReplicaIds.Dequeue();

            //#TODO overflow
            return _nextLocalReplicaId++;
        }

        public void FreeLocalReplicaId(ushort localId) {
            Assert.IsTrue(localId < _nextLocalReplicaId);
            _replicaIdRecycleQueue.Enqueue(localId);
        }

        void OnReplicaRpc(Connection connection, BitStream bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG
                Debug.LogError("Replica with id " + replicaId + " missing on server");
#endif
                return;
            }

            ReplicaBehaviour.CallRpc(replica, connection, bs, this);
        }
    }
#endif
}