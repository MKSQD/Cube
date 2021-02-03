using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using UnityEngine.SceneManagement;
using Cube.Transport;
using BitStream = Cube.Transport.BitStream;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

namespace Cube.Replication {
    [Serializable]
    public class ServerReplicaManagerSettings {
        [FormerlySerializedAs("MaxBytesPerConnectionPerUpdate")]
        [Range(0, 1500)]
        public int MaxBytesPerConnectionPerUpdate = 1400;

        [FormerlySerializedAs("MaxBytesPerConnectionPerUpdate")]
        [Range(1000f / 60f, 1000f / 1f)]
        public float ReplicaUpdateRateMS = 33; // 30 times per second
        public float ReplicaUpdateRate {
            get { return ReplicaUpdateRateMS * 0.001f; }
        }

        [Range(1000f / 60f, 1000f / 0.1f)]
        public float ReplicaRelevantSetUpdateRateMS = 1000;
        public float ReplicaRelevantSetUpdateRate {
            get { return ReplicaRelevantSetUpdateRateMS * 0.001f; }
        }

        [Tooltip("Min. relevance for a Replica to even be considered for replication")]
        [Range(0f, 1f)]
        public float MinRelevance = 0.3f;
    }

    public class ServerReplicaManagerStatistics {
        public struct ReplicaTypeInfo {
            public int NumInstances;
            public int TotalBytes;
            public int NumRpcs;
            public int RpcBytes;
        }

        public class ReplicaViewInfo {
            public Dictionary<int, ReplicaTypeInfo> ReplicaTypeInfos = new Dictionary<int, ReplicaTypeInfo>();
        }

        public struct ReplicaViewInfoPair {
            public ReplicaView View;
            public ReplicaViewInfo Info;
        }

        public List<ReplicaViewInfoPair> ViewInfos = new List<ReplicaViewInfoPair>();
    }

    public sealed class ServerReplicaManager : IServerReplicaManager {
#if UNITY_EDITOR
        public static ServerReplicaManager Main;
#endif

        const ushort FirstLocalReplicaId = 255; // The first 255 values are reserved for scene Replicas

        ICubeServer server;

        NetworkScene networkScene;

        [SerializeField]
        List<ReplicaView> replicaViews = new List<ReplicaView>();
        public List<ReplicaView> ReplicaViews {
            get { return replicaViews; }
        }

        ServerReplicaManagerSettings settings;

        double nextUpdateTime;

        ushort nextLocalReplicaId = FirstLocalReplicaId;
        Queue<ushort> freeReplicaIds = new Queue<ushort>();

        float nextReplicaIdRecycleTime = 0;
        Queue<ushort> replicaIdRecycleQueue = new Queue<ushort>();

        Dictionary<ReplicaId, Replica> replicasInConstruction = new Dictionary<ReplicaId, Replica>();
        List<Replica> replicasInDestruction = new List<Replica>();

#if UNITY_EDITOR
        ServerReplicaManagerStatistics _statistic;
        public ServerReplicaManagerStatistics Statistics {
            get { return _statistic; }
        }
#endif

        public ServerReplicaManager(ICubeServer server, ServerReplicaManagerSettings settings) {
            Assert.IsNotNull(server);
            Assert.IsNotNull(settings);

            networkScene = new NetworkScene();

            this.server = server;
            server.reactor.AddMessageHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);

            this.settings = settings;

            SceneManager.sceneLoaded += OnSceneLoaded;

#if UNITY_EDITOR
            Assert.IsNull(Main);
            Main = this;
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
                replica.server = server;
                networkScene.AddReplica(replica);
            }
        }

        public void Reset() {
            networkScene.DestroyAll();

            foreach (var idReplicaPair in replicasInConstruction) {
                UnityEngine.Object.Destroy(idReplicaPair.Value.gameObject);
            }
            replicasInConstruction.Clear();

            freeReplicaIds.Clear();
            nextLocalReplicaId = FirstLocalReplicaId;
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

            var newInstance = UnityEngine.Object.Instantiate(prefab, position, rotation, server.world.transform);
            var replica = InstantiateReplicaImpl(newInstance);
            if (replica == null) {
                Debug.LogError("Prefab <i>" + prefab + "</i> is missing Replica Component", prefab);
                return null;
            }

            return newInstance;
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key) {
            return InstantiateReplicaAsync(key, Vector3.zero, Quaternion.identity);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position) {
            return InstantiateReplicaAsync(key, position, Quaternion.identity);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position, Quaternion rotation) {
            var newInstance = Addressables.InstantiateAsync(key, position, rotation, server.world.transform);
            newInstance.Completed += obj => {
                var replica = InstantiateReplicaImpl(obj.Result);
                if (replica == null) {
                    Debug.LogError("Prefab <i>" + key + "</i> is missing Replica Component");
                }
            };

            return newInstance;
        }

        Replica InstantiateReplicaImpl(GameObject newInstance) {
            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null)
                return null;

            newReplica.server = server;
            newReplica.ReplicaId = ReplicaId.Create(this);
            Assert.IsTrue(newReplica.ReplicaId != ReplicaId.Invalid);
            newReplica.TakeOwnership();

            // Wait for one frame until Start is called before replicating to clients
            replicasInConstruction[newReplica.ReplicaId] = newReplica;

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

            replicasInConstruction.Remove(replica.ReplicaId);
            networkScene.RemoveReplica(replica);

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
            bool alreadyConstructed = !replicasInConstruction.Remove(replica.ReplicaId);
            if (!alreadyConstructed) {
                networkScene.RemoveReplica(replica);
                replica.ReplicaId = ReplicaId.Invalid;
                UnityEngine.Object.Destroy(replica.gameObject);
                return;
            }

            replicasInDestruction.Add(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            return networkScene.GetReplicaById(id);
        }

        public void Update() {
            RecycleReplicaIdsAfterDelay();

            // Update set of relevant Replicas
            for (int i = 0; i < replicaViews.Count; ++i) {
                var replicaView = replicaViews[i];
                if (replicaView.IsLoadingLevel)
                    continue;

                if (Time.timeAsDouble >= replicaView.NextPriorityUpdateTime) {
                    replicaView.NextPriorityUpdateTime = Time.timeAsDouble + settings.ReplicaRelevantSetUpdateRate;
                    UpdateRelevantReplicas(replicaView);
                }
            }

            // Network tick
            if (Time.timeAsDouble >= nextUpdateTime) {
                nextUpdateTime = Time.timeAsDouble + settings.ReplicaUpdateRate;

#if UNITY_EDITOR
                _statistic = new ServerReplicaManagerStatistics();
#endif

                for (int i = 0; i < replicaViews.Count; ++i) {
                    var replicaView = replicaViews[i];
                    if (replicaView.IsLoadingLevel)
                        continue;

                    UpdateReplicaView(replicaView);
                }

                foreach (var replica in networkScene.replicas) {
                    replica.queuedRpcs.Clear();
                }

                foreach (var idReplicaPair in replicasInConstruction) {
                    var replica = idReplicaPair.Value;
                    networkScene.AddReplica(replica);
                }
                replicasInConstruction.Clear();
            }

            // Actually destroy queued Replicas
            if (replicasInDestruction.Count > 0) {
                try {
                    for (int i = 0; i < replicaViews.Count; ++i) {
                        var replicaView = replicaViews[i];
                        if (replicaView.IsLoadingLevel)
                            continue;

                        SendDestroyedReplicasToReplicaView(replicaView);
                    }

                    foreach (var replica in replicasInDestruction) {
                        FreeLocalReplicaId(replica.ReplicaId);

                        networkScene.RemoveReplica(replica);
                        replica.ReplicaId = ReplicaId.Invalid;
                        UnityEngine.Object.Destroy(replica.gameObject);
                    }
                }
                finally {
                    replicasInDestruction.Clear();
                }
            }
        }

        public void ForceReplicaViewRefresh(ReplicaView view) {
            Assert.IsTrue(!view.IsLoadingLevel);

            UpdateRelevantReplicas(view);
        }

        void RecycleReplicaIdsAfterDelay() {
            if (Time.time < nextReplicaIdRecycleTime)
                return;

            if (replicaIdRecycleQueue.Count > 0) {
                var id = replicaIdRecycleQueue.Dequeue();
                freeReplicaIds.Enqueue(id);
            }

            nextReplicaIdRecycleTime = Time.time + Constants.ServerReplicaIdRecycleTime;
        }

        void UpdateReplicaView(ReplicaView view) {
            UpdateRelevantReplicaPriorities(view);

#if UNITY_EDITOR
            var perReplicaViewInfo = new ServerReplicaManagerStatistics.ReplicaViewInfo();
            _statistic.ViewInfos.Add(new ServerReplicaManagerStatistics.ReplicaViewInfoPair { View = view, Info = perReplicaViewInfo });
#endif

            int bytesSent = 0;

            var sortedIndices = new List<int>();
            CalculateRelevantReplicaIndices(view, sortedIndices);

            var serializeCtx = new ReplicaBehaviour.SerializeContext() {
                Observer = view
            };

            foreach (var currentReplicaIdx in sortedIndices) {
                var replica = view.RelevantReplicas[currentReplicaIdx];
                if (replica == null || replica.ReplicaId == ReplicaId.Invalid)
                    continue;

                var updateBs = server.networkInterface.bitStreamPool.Create();
                updateBs.Write((byte)MessageId.ReplicaUpdate);
                updateBs.Write(replica.isSceneReplica);
                if (!replica.isSceneReplica) {
                    updateBs.Write(replica.prefabIdx);
                }

                updateBs.Write(replica.ReplicaId);

                bool isOwner = replica.Owner == view.Connection;
                updateBs.Write(isOwner);

                replica.Serialize(updateBs, serializeCtx);

                server.networkInterface.SendBitStream(updateBs, PacketPriority.Medium, PacketReliability.Unreliable, view.Connection);

                bytesSent += updateBs.Length;

                // We just sent this Replica, reset its priority
                view.RelevantReplicaPriorityAccumulator[currentReplicaIdx] = 0;

#if UNITY_EDITOR
                // Add some profiling info
                perReplicaViewInfo.ReplicaTypeInfos.TryGetValue(replica.prefabIdx, out ServerReplicaManagerStatistics.ReplicaTypeInfo replicaTypeInfo);
                ++replicaTypeInfo.NumInstances;
                replicaTypeInfo.TotalBytes += updateBs.Length;
                replicaTypeInfo.NumRpcs += replica.queuedRpcs.Count;
                replicaTypeInfo.RpcBytes += replica.queuedRpcs.Sum(rpc => rpc.bs.Length);

                perReplicaViewInfo.ReplicaTypeInfos[replica.prefabIdx] = replicaTypeInfo;
#endif

                if (bytesSent >= settings.MaxBytesPerConnectionPerUpdate)
                    return; // Packet size exhausted

                // Rpcs
                foreach (var queuedRpc in replica.queuedRpcs) {
                    if ((queuedRpc.target == RpcTarget.Owner && replica.Owner == view.Connection)
                        || queuedRpc.target == RpcTarget.All
                        || (queuedRpc.target == RpcTarget.AllClientsExceptOwner && replica.Owner != view.Connection)) {
                        server.networkInterface.SendBitStream(queuedRpc.bs, PacketPriority.Low, PacketReliability.Unreliable, view.Connection);

                        bytesSent += queuedRpc.bs.Length;

                        if (bytesSent >= settings.MaxBytesPerConnectionPerUpdate)
                            return; // Packet size exhausted
                    }
                }
            }
        }

        /// <summary>
        /// Build a list of relevant Replicas for this ReplicaView.
        /// </summary>
        void UpdateRelevantReplicas(ReplicaView view) {
            if (view.RelevantReplicaPriorityAccumulator == null) {
                view.RelevantReplicaPriorityAccumulator = new List<float>();
            }
            if (view.RelevantReplicas == null) {
                view.RelevantReplicas = new List<Replica>();
            }

            var oldAccs = new Dictionary<Replica, float>();
            for (int i = 0; i < view.RelevantReplicas.Count; ++i) {
                var replica = view.RelevantReplicas[i];
                if (replica == null)
                    continue;

                oldAccs.Add(replica, view.RelevantReplicaPriorityAccumulator[i]);
            }

            GatherRelevantReplicas(networkScene.replicas, view, view.RelevantReplicas);

            view.RelevantReplicaPriorityAccumulator.Clear();
            foreach (var replica in view.RelevantReplicas) {
                oldAccs.TryGetValue(replica, out float acc);
                view.RelevantReplicaPriorityAccumulator.Add(acc);
            }
        }

        void GatherRelevantReplicas(ReadOnlyCollection<Replica> replicas, ReplicaView view, List<Replica> relevantReplicas) {
            relevantReplicas.Clear();
            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (replica == null)
                    continue;

                if (!replica.IsRelevantFor(view))
                    continue;

                var relevance = replica.GetRelevance(view);
                if (relevance < settings.MinRelevance)
                    continue;

                relevantReplicas.Add(replica);
            }
        }

        void UpdateRelevantReplicaPriorities(ReplicaView view) {
            // This function is called with a rate of settings.replicaUpdateRateMS

            for (int i = 0; i < view.RelevantReplicas.Count; ++i) {
                var replica = view.RelevantReplicas[i];
                if (replica == null)
                    continue;

                var relevance = replica.GetRelevance(view);
                Assert.IsTrue(relevance >= 0 && relevance <= 1);

                var a = (settings.ReplicaUpdateRateMS / replica.settings.DesiredUpdateRateMS) * relevance;
                view.RelevantReplicaPriorityAccumulator[i] += a;
            }
        }

        static void CalculateRelevantReplicaIndices(ReplicaView view, List<int> sortedReplicaIndices) {
            sortedReplicaIndices.Clear();

            for (int i = 0; i < view.RelevantReplicas.Count; ++i) {
                if (view.RelevantReplicaPriorityAccumulator[i] < 1)
                    continue;

                sortedReplicaIndices.Add(i);
            }

            sortedReplicaIndices.Sort((i1, i2) => (int)((view.RelevantReplicaPriorityAccumulator[i2] - view.RelevantReplicaPriorityAccumulator[i1]) * 100));
        }

        void SendDestroyedReplicasToReplicaView(ReplicaView view) {
            Assert.IsTrue(replicasInDestruction.Count > 0);

            var destroyBs = server.networkInterface.bitStreamPool.Create();
            destroyBs.Write((byte)MessageId.ReplicaDestroy);

            foreach (var replica in replicasInDestruction) {
                var wasInterestedInReplica = view.RelevantReplicas.Contains(replica);
                if (!wasInterestedInReplica)
                    continue;

                destroyBs.Write(replica.ReplicaId);

                // Serialize custom destruction data
                var replicaDestructionBs = server.networkInterface.bitStreamPool.Create();
                replica.SerializeDestruction(replicaDestructionBs, new ReplicaBehaviour.SerializeContext() {
                    Observer = view
                });

                var absOffset = (ushort)(destroyBs.LengthInBits + 16 + replicaDestructionBs.LengthInBits);
                destroyBs.Write(absOffset);
                destroyBs.Write(replicaDestructionBs);
                destroyBs.AlignWriteToByteBoundary();
            }

            server.networkInterface.SendBitStream(destroyBs, PacketPriority.Medium, PacketReliability.Unreliable, view.Connection);
        }

        public ReplicaView GetReplicaView(Connection connection) {
            foreach (var replicaView in replicaViews) {
                if (replicaView.Connection == connection)
                    return replicaView;
            }
            return null;
        }

        public void AddReplicaView(ReplicaView view) {
            if (replicaViews.Contains(view))
                return;

            if (view.Connection == Connection.Invalid)
                throw new ArgumentException("connection");

            replicaViews.Add(view);
        }

        public void RemoveReplicaView(Connection connection) {
            for (int i = 0; i < replicaViews.Count; ++i) {
                if (replicaViews[i].Connection == connection) {
                    replicaViews.RemoveAt(i);
                    return;
                }
            }
        }

        public ushort AllocateLocalReplicaId() {
            if (freeReplicaIds.Count > 0)
                return freeReplicaIds.Dequeue();

            return nextLocalReplicaId++;
        }

        public void FreeLocalReplicaId(ReplicaId id) {
            if (id.data >= nextLocalReplicaId)
                return; // Tried to free id after Reset() was called

            replicaIdRecycleQueue.Enqueue(id.data);
        }

        void OnReplicaRpc(Connection connection, BitStream bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = networkScene.GetReplicaById(replicaId);
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