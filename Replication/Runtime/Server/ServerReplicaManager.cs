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

    public class ServerReplicaManagerStatistics {
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

        public List<ReplicaViewInfoPair> ViewInfos = new List<ReplicaViewInfoPair>();
    }

    public sealed class ServerReplicaManager : IServerReplicaManager {
#if UNITY_EDITOR
        public static List<ServerReplicaManager> all = new List<ServerReplicaManager>();
#endif

        const ushort FirstLocalReplicaId = 255; // The first 255 values are reserved for scene Replicas

        ICubeServer _server;

        NetworkScene _networkScene;

        [SerializeField]
        List<ReplicaView> _replicaViews = new List<ReplicaView>();
        public List<ReplicaView> ReplicaViews {
            get { return _replicaViews; }
        }

        ServerReplicaManagerSettings _settings;

        float _nextUpdateTime;
        float _nextPriorityUpdateTime;

        ushort _nextLocalReplicaId = FirstLocalReplicaId;
        Queue<ushort> _freeReplicaIds = new Queue<ushort>();

        float _nextReplicaIdRecycleTime = 0;
        Queue<ushort> _replicaIdRecycleQueue = new Queue<ushort>();

        Dictionary<ReplicaId, Replica> _replicasInConstruction = new Dictionary<ReplicaId, Replica>();
        List<Replica> _replicasInDestruction = new List<Replica>();

#if UNITY_EDITOR
        ServerReplicaManagerStatistics _statistic;
        public ServerReplicaManagerStatistics Statistics {
            get { return _statistic; }
        }
#endif

        public ServerReplicaManager(ICubeServer server, ServerReplicaManagerSettings settings) {
            Assert.IsNotNull(server);
            Assert.IsNotNull(settings);

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
                    if (!replica.isSceneReplica)
                        continue;

                    sceneReplicas.Add(replica);
                }
            }

            sceneReplicas.Sort((r1, r2) => r1.sceneIdx - r2.sceneIdx);

            foreach (var replica in sceneReplicas) {
                replica.ReplicaId = ReplicaId.CreateFromExisting(replica.sceneIdx);
                replica.server = _server;
                _networkScene.AddReplica(replica);
            }
        }

        public void Reset() {
            _networkScene.DestroyAll();

            foreach (var idReplicaPair in _replicasInConstruction) {
                UnityEngine.Object.Destroy(idReplicaPair.Value.gameObject);
            }
            _replicasInConstruction.Clear();

            _freeReplicaIds.Clear();
            _nextLocalReplicaId = FirstLocalReplicaId;
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
            if (replica == null)
                return null;

            replica.TakeOwnership();

            return replica.gameObject;
        }

        Replica InstantiateReplicaImpl(GameObject prefab, Vector3 position, Quaternion rotation) {
            var newInstance = UnityEngine.Object.Instantiate(prefab, position, rotation, _server.world.transform);

            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null) {
                Debug.LogError("Prefab <i>" + prefab + "</i> is missing Replica Component", prefab);
                return null;
            }

            newReplica.server = _server;
            newReplica.ReplicaId = ReplicaId.Create(this);
            Assert.IsTrue(newReplica.ReplicaId != ReplicaId.Invalid);

            // Wait for one frame until Start is called before replicating to clients
            _replicasInConstruction[newReplica.ReplicaId] = newReplica;

            return newReplica;
        }

        /// <summary>
        /// Remove the Replica instantly from the manager, without notifying the clients.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        public void RemoveReplica(Replica replica) {
            if (replica.ReplicaId == ReplicaId.Invalid)
                return; // Replica already removed

            _replicasInConstruction.Remove(replica.ReplicaId);
            _networkScene.RemoveReplica(replica);

            if (replica.ReplicaId != ReplicaId.Invalid) {
                FreeLocalReplicaId(replica.ReplicaId);
            }

            replica.ReplicaId = ReplicaId.Invalid;
        }

        /// <summary>
        /// Removes the Replica instantly from this manager, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        public void DestroyReplica(Replica replica) {
            if (replica.ReplicaId == ReplicaId.Invalid)
                return; // Replica already destroyed

            // We don't need to send a destroy message if the replica is not yet fully constructed on the server
            bool alreadyConstructed = !_replicasInConstruction.Remove(replica.ReplicaId);
            if (!alreadyConstructed) {
                _networkScene.RemoveReplica(replica);
                replica.ReplicaId = ReplicaId.Invalid;
                UnityEngine.Object.Destroy(replica.gameObject);
                return;
            }

            _replicasInDestruction.Add(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            return _networkScene.GetReplicaById(id);
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
                _statistic = new ServerReplicaManagerStatistics();
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

                foreach (var idReplicaPair in _replicasInConstruction) {
                    var replica = idReplicaPair.Value;
                    _networkScene.AddReplica(replica);
                }
                _replicasInConstruction.Clear();
            }

            // Actually destroy queued Replicas
            if (_replicasInDestruction.Count > 0) {
                try {
                    for (int i = 0; i < _replicaViews.Count; ++i) {
                        var replicaView = _replicaViews[i];
                        if (replicaView.isLoadingLevel)
                            continue;

                        SendDestroyedReplicasToReplicaView(replicaView);
                    }

                    foreach (var replica in _replicasInDestruction) {
                        FreeLocalReplicaId(replica.ReplicaId);

                        _networkScene.RemoveReplica(replica);
                        replica.ReplicaId = ReplicaId.Invalid;
                        UnityEngine.Object.Destroy(replica.gameObject);
                    }
                }
                finally {
                    _replicasInDestruction.Clear();
                }
            }
        }

        public void ForceReplicaViewRefresh(ReplicaView view) {
            Assert.IsTrue(!view.isLoadingLevel);

            UpdateRelevantReplicas(view);
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

        void UpdateReplicaView(ReplicaView view) {
            UpdateRelevantReplicaPriorities(view);

#if UNITY_EDITOR
            var perReplicaViewInfo = new ServerReplicaManagerStatistics.ReplicaViewInfo();
            _statistic.ViewInfos.Add(new ServerReplicaManagerStatistics.ReplicaViewInfoPair { view = view, info = perReplicaViewInfo });
#endif

            int bytesSent = 0;

            var sortedIndices = GetSortedRelevantReplicaIndices(view);
            var serializeCtx = new ReplicaBehaviour.SerializeContext() {
                Observer = view
            };

            foreach (var idx in sortedIndices) {
                var replica = view.relevantReplicas[idx];
                if (replica == null || replica.ReplicaId == ReplicaId.Invalid)
                    continue;

                var updateBs = _server.networkInterface.bitStreamPool.Create();
                updateBs.Write((byte)MessageId.ReplicaUpdate);
                updateBs.Write(replica.isSceneReplica);
                if (!replica.isSceneReplica) {
                    updateBs.Write(replica.prefabIdx);
                }

                updateBs.Write(replica.ReplicaId);

                bool isOwner = replica.Owner == view.connection;
                updateBs.Write(isOwner);

                replica.Serialize(updateBs, serializeCtx);

                _server.networkInterface.SendBitStream(updateBs, PacketPriority.Medium, PacketReliability.Unreliable, view.connection);

                bytesSent += updateBs.Length;

                // We just sent this Replica, reset its priority
                view.relevantReplicaPriorityAccumulator[idx] = 0;

#if UNITY_EDITOR
                // Add some profiling info
                perReplicaViewInfo.replicaTypeInfos.TryGetValue(replica.prefabIdx, out ServerReplicaManagerStatistics.ReplicaTypeInfo replicaTypeInfo);
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
                    if ((queuedRpc.target == RpcTarget.Owner && replica.Owner == view.connection)
                        || queuedRpc.target == RpcTarget.All
                        || (queuedRpc.target == RpcTarget.AllClientsExceptOwner && replica.Owner != view.connection)) {
                        _server.networkInterface.SendBitStream(queuedRpc.bs, PacketPriority.Low, PacketReliability.Unreliable, view.connection);

                        bytesSent += queuedRpc.bs.Length;

                        if (bytesSent >= _settings.maxBytesPerConnectionPerUpdate)
                            return; // Packet size exhausted
                    }
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

                var a = replica.GetPriorityFor(view) * (2048 / replica.settings.desiredUpdateRateMs);
                view.relevantReplicaPriorityAccumulator[i] += a;
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
            Assert.IsTrue(_replicasInDestruction.Count > 0);

            var destroyBs = _server.networkInterface.bitStreamPool.Create();
            destroyBs.Write((byte)MessageId.ReplicaDestroy);

            foreach (var replica in _replicasInDestruction) {
                var wasInterestedInReplica = view.relevantReplicas.Contains(replica);
                if (!wasInterestedInReplica)
                    continue;

                destroyBs.Write(replica.ReplicaId);

                // Serialize custom destruction data
                var replicaDestructionBs = _server.networkInterface.bitStreamPool.Create();
                replica.SerializeDestruction(replicaDestructionBs, new ReplicaBehaviour.SerializeContext() {
                        Observer = view
                });

                var absOffset = (ushort)(destroyBs.LengthInBits + 16 + replicaDestructionBs.LengthInBits);
                destroyBs.Write(absOffset);
                destroyBs.Write(replicaDestructionBs);
                destroyBs.AlignWriteToByteBoundary();
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

            return _nextLocalReplicaId++;
        }

        public void FreeLocalReplicaId(ReplicaId id) {
            if (id.data >= _nextLocalReplicaId)
                return; // Tried to free id after Reset() was called

            _replicaIdRecycleQueue.Enqueue(id.data);
        }

        void OnReplicaRpc(Connection connection, BitStream bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG_REP
                Debug.LogWarning("Received RPC for invalid Replica <i>" + replicaId + "</i>");
#endif
                return;
            }

            replica.CallRpcServer(connection, bs, this);
        }
    }
}