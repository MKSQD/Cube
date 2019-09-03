using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using UnityEngine.SceneManagement;
using Cube.Transport;
using BitStream = Cube.Transport.BitStream;
using System.Linq;

namespace Cube.Replication {
    [Serializable]
    public class ServerReplicaManagerSettings {
        [Range(0, 1500)]
        public int maxBytesPerConnectionPerUpdate = 1400;
        [Range(10, 1000)]
        public float replicaUpdateRateMS = 33; // 30 times per second
    }

    public sealed class ServerReplicaManager : IServerReplicaManager {
#if UNITY_EDITOR
        public static List<ServerReplicaManager> all = new List<ServerReplicaManager>();
#endif

        public class Statistic {
            public struct ReplicaTypeInfo {
                public int numInstances;
                public int totalBytes;
                public int numRpcs;
                public int rpcBytes;
            }

            public class ReplicaViewInfo {
                public Dictionary<int, ReplicaTypeInfo> replicaTypeInfos = new Dictionary<int, ReplicaTypeInfo>();
            }

            public struct ReplicaViewInfoPair {
                public ReplicaView view;
                public ReplicaViewInfo info;
            }

            public List<ReplicaViewInfoPair> viewInfos = new List<ReplicaViewInfoPair>();
        }

        ICubeServer _server;

        NetworkScene _networkScene;

        Transform _serverTransform;
        public Transform instantiateTransform {
            get { return _serverTransform; }
        }

        [SerializeField]
        List<ReplicaView> _replicaViews = new List<ReplicaView>();
        public List<ReplicaView> replicaViews {
            get { return _replicaViews; }
        }

        ServerReplicaManagerSettings _settings;

        float _nextUpdateTime;
        float _nextPriorityUpdateTime;

        ushort _nextLocalReplicaId = 255; // The first 255 values are reserved for scene Replicas
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

        public ServerReplicaManager(ICubeServer server, Transform serverTransform, ServerReplicaManagerSettings settings) {
            Assert.IsNotNull(server);
            Assert.IsNotNull(serverTransform);
            Assert.IsNotNull(settings);

            _serverTransform = serverTransform;

            _networkScene = new NetworkScene();

            _server = server;
            server.reactor.AddMessageHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);

            _settings = settings;

            SceneManager.sceneLoaded += OnSceneLoaded;

#if UNITY_EDITOR
            all.Add(this);
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    if (replica.sceneIdx == 0)
                        continue;

                    sceneReplicas.Add(replica);
                }
            }

            sceneReplicas.Sort((r1, r2) => r1.sceneIdx - r2.sceneIdx);

            foreach (var replica in sceneReplicas) {
                replica.id = ReplicaId.CreateFromExisting(replica.sceneIdx);
                replica.server = _server;
                _networkScene.AddReplica(replica);
            }
        }

        public void Reset() {
            _networkScene.DestroyAll();

            foreach (var pair in _constructingReplicas) {
                UnityEngine.Object.Destroy(pair.Value.gameObject);
            }
            _constructingReplicas.Clear();

            _freeReplicaIds.Clear();
            _nextLocalReplicaId = 255;
        }

        public GameObject InstantiateReplica(GameObject prefab) {
            return InstantiateReplica(prefab, Vector3.zero, Quaternion.identity);
        }

        public GameObject InstantiateReplica(GameObject prefab, Vector3 position) {
            return InstantiateReplica(prefab, position, Quaternion.identity);
        }

        public GameObject InstantiateReplica(GameObject prefab, Vector3 position, Quaternion rotation) {
            if (prefab == null)
                throw new ArgumentNullException("prefab");

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

            newReplica.server = _server;
            newReplica.id = ReplicaId.Create(this);

            //Delay sending to client because we should wait for one frame until Start is called.
            _constructingReplicas[newReplica.id] = newReplica;

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

            if (Time.time >= _nextPriorityUpdateTime) {
                _nextPriorityUpdateTime = Time.time + 1;

                for (int i = 0; i < _replicaViews.Count; ++i) {
                    var replicaView = _replicaViews[i];
                    if (replicaView.isLoadingLevel)
                        continue;

                    UpdateRelevantReplicas(replicaView);
                }
            }

            if (Time.time >= _nextUpdateTime) {
                _nextUpdateTime = Time.time + _settings.replicaUpdateRateMS * 0.001f;

#if UNITY_EDITOR
                _statistic = new Statistic();
#endif

                for (int i = 0; i < _replicaViews.Count; ++i) {
                    var replicaView = _replicaViews[i];
                    if (replicaView.isLoadingLevel)
                        continue;

                    UpdateReplicaView(replicaView);
                }

                foreach (var replica in _networkScene.replicas) {
                    replica.queuedRpcs.Clear();
                }

                foreach (var replicaId in _destroyedReplicas) {
                    FreeLocalReplicaId(replicaId.data);
                }
                _destroyedReplicas.Clear();

                foreach (var entry in _constructingReplicas) {
                    _networkScene.AddReplica(entry.Value);
                }
                _constructingReplicas.Clear();
            }
        }

        public void ForceReplicaViewRefresh(ReplicaView view) {
            Assert.IsTrue(!view.isLoadingLevel);

            UpdateRelevantReplicas(view);
        }

        void UpdateReplicaView(ReplicaView view) {
            SendDestroyedReplicasToReplicaView(view);
            UpdateRelevantReplicaPriorities(view);

#if UNITY_EDITOR
            var perReplicaViewInfo = new Statistic.ReplicaViewInfo();
            _statistic.viewInfos.Add(new Statistic.ReplicaViewInfoPair { view = view, info = perReplicaViewInfo });
#endif

            int bytesSent = 0;

            var sortedIndices = GetSortedRelevantReplicaIndices(view);

            int numUpdatesSent = 0;
            int numRpcsSent = 0;
            foreach (var idx in sortedIndices) {
                var replica = view.relevantReplicas[idx];
                if (replica == null || replica.id == ReplicaId.Invalid)
                    continue;

                var updateBs = _server.networkInterface.bitStreamPool.Create();
                updateBs.Write((byte)MessageId.ReplicaUpdate);

                bool isOwner = view.connection == replica.owner;
                updateBs.Write(isOwner);

                updateBs.Write(replica.isSceneReplica);
                if (!replica.isSceneReplica) {
                    updateBs.Write(replica.prefabIdx);
                }

                updateBs.Write(replica.id);

                foreach (var component in replica.replicaBehaviours) {
                    component.Serialize(updateBs, view);
                }

                _server.networkInterface.SendBitStream(updateBs, PacketPriority.Medium, PacketReliability.Unreliable, view.connection);


                ++numUpdatesSent;
                bytesSent += updateBs.Length;

                // We just sent this Replica, reset its priority
                view.relevantReplicaPriorityAccumulator[idx] = 0;

#if UNITY_EDITOR
                // Add some profiling info
                perReplicaViewInfo.replicaTypeInfos.TryGetValue(replica.prefabIdx, out Statistic.ReplicaTypeInfo replicaTypeInfo);
                ++replicaTypeInfo.numInstances;
                replicaTypeInfo.totalBytes += updateBs.Length;
                replicaTypeInfo.numRpcs += replica.queuedRpcs.Count;
                replicaTypeInfo.rpcBytes += replica.queuedRpcs.Sum(rpc => rpc.bs.Length);

                perReplicaViewInfo.replicaTypeInfos[replica.prefabIdx] = replicaTypeInfo;
#endif

                if (bytesSent >= _settings.maxBytesPerConnectionPerUpdate)
                    return; // Packet size exhausted

                // Rpcs
                foreach (var queuedRpc in replica.queuedRpcs) {
                    if (queuedRpc.target == RpcTarget.Owner && replica.owner == view.connection) {
                        _server.networkInterface.SendBitStream(queuedRpc.bs, PacketPriority.Low, PacketReliability.Unreliable, replica.owner);
                    }
                    else if (queuedRpc.target == RpcTarget.All) {
                        _server.networkInterface.BroadcastBitStream(queuedRpc.bs, PacketPriority.Low, PacketReliability.Unreliable);
                    }

                    ++numRpcsSent;

                    bytesSent += queuedRpc.bs.Length;
                    if (bytesSent >= _settings.maxBytesPerConnectionPerUpdate)
                        return;
                }
            }
        }

        void UpdateRelevantReplicas(ReplicaView view) {
            var oldAccs = new Dictionary<Replica, float>();
            if (view.relevantReplicaPriorityAccumulator != null) {
                for (int i = 0; i < view.relevantReplicas.Count; ++i) {
                    var replica = view.relevantReplicas[i];
                    if (replica == null)
                        continue;

                    oldAccs.Add(replica, view.relevantReplicaPriorityAccumulator[i]);
                }
            }

            view.relevantReplicas = GatherRelevantReplicas(_networkScene.replicas, view);

            if (view.relevantReplicaPriorityAccumulator == null) {
                view.relevantReplicaPriorityAccumulator = new List<float>();
            }
            view.relevantReplicaPriorityAccumulator.Clear();

            for (int i = 0; i < view.relevantReplicas.Count; ++i) {
                var replica = view.relevantReplicas[i];

                var acc = 0f;
                oldAccs.TryGetValue(replica, out acc);

                view.relevantReplicaPriorityAccumulator.Add(acc);
            }
        }

        static List<Replica> GatherRelevantReplicas(ReadOnlyCollection<Replica> replicas, ReplicaView view) {
            var minPriority = 0.3f;

            var result = new List<Replica>(32);
            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (replica == null)
                    continue;

                if (!replica.IsRelevantFor(view))
                    continue;

                var priority = replica.GetPriorityFor(view);
                if (priority < minPriority)
                    continue;

                result.Add(replica);
            }

            return result;
        }

        static void UpdateRelevantReplicaPriorities(ReplicaView view) {
            if (view.relevantReplicas == null)
                return;

            for (int i = 0; i < view.relevantReplicas.Count; ++i) {
                var replica = view.relevantReplicas[i];
                if (replica == null)
                    continue;

                view.relevantReplicaPriorityAccumulator[i] += replica.GetPriorityFor(view) * (2048 / replica.settings.desiredUpdateRateMs);
            }
        }

        static List<int> GetSortedRelevantReplicaIndices(ReplicaView view) {
            if (view.relevantReplicas == null)
                return new List<int>();

            var sortedIndices = Enumerable.Range(0, view.relevantReplicas.Count).ToList();
            sortedIndices.Sort((i1, i2) => (int)((view.relevantReplicaPriorityAccumulator[i2] - view.relevantReplicaPriorityAccumulator[i1]) * 100));

            return sortedIndices;
        }

        void SendDestroyedReplicasToReplicaView(ReplicaView view) {
            if (_destroyedReplicas.Count == 0)
                return;

            var destroyBs = _server.networkInterface.bitStreamPool.Create();
            destroyBs.Write((byte)MessageId.ReplicaDestroy);
            destroyBs.Write((byte)_destroyedReplicas.Count);

            foreach (var id in _destroyedReplicas) {
                destroyBs.Write(id);
            }

            _server.networkInterface.SendBitStream(destroyBs, PacketPriority.Medium, PacketReliability.Unreliable, view.connection);
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
            if (localId >= _nextLocalReplicaId)
                return; // Tried to free id after Reset() was called

            _replicaIdRecycleQueue.Enqueue(localId);
        }

        void OnReplicaRpc(Connection connection, BitStream bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG_REP
                Debug.LogError("Replica with id " + replicaId + " missing on server");
#endif
                return;
            }

            replica.CallRpcServer(connection, bs, this);
        }
    }
}