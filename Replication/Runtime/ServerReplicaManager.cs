using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cube.Transport;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    public sealed class ServerReplicaManager : IServerReplicaManager {
#if UNITY_EDITOR
        public static ServerReplicaManager Main;
#endif

        const ushort FirstLocalReplicaId = 255; // The first 255 values are reserved for scene Replicas

        readonly ICubeServer server;
        readonly Transform spawnTransform;
        readonly NetworkScene networkScene;
        public ReadOnlyCollection<Replica> Replicas => networkScene.Replicas;

        [SerializeField]
        List<ReplicaView> replicaViews = new List<ReplicaView>();
        public List<ReplicaView> ReplicaViews => replicaViews;

        readonly ServerReplicaManagerSettings settings;

        double nextUpdateTime;

        ushort nextLocalReplicaId = FirstLocalReplicaId;
        readonly Queue<ushort> freeReplicaIds = new Queue<ushort>();

        float nextReplicaIdRecycleTime = 0;
        readonly Queue<ushort> replicaIdRecycleQueue = new Queue<ushort>();

        Dictionary<ReplicaId, Replica> replicasInConstruction = new Dictionary<ReplicaId, Replica>();
        List<Replica> replicasInDestruction = new List<Replica>();

        public ServerReplicaManager(ICubeServer server, Transform spawnTransform, ServerReplicaManagerSettings settings) {
            Assert.IsNotNull(server);
            Assert.IsNotNull(spawnTransform);
            Assert.IsNotNull(settings);

            networkScene = new NetworkScene();
            this.spawnTransform = spawnTransform;

            this.server = server;
            server.Reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);

            this.settings = settings;

            SceneManager.sceneLoaded += (scene, mode) => ProcessSceneReplicasInScene(scene);

#if UNITY_EDITOR
            Main = this;
#endif
        }

        /// Scan a newly loaded Scene for scene Replicas.
        void ProcessSceneReplicasInScene(Scene scene) {
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
                replica.Id = ReplicaId.CreateFromExisting(replica.sceneIdx);
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

            var newInstance = UnityEngine.Object.Instantiate(prefab, position, rotation, spawnTransform);
            var replica = InstantiateReplicaImpl(newInstance);
            if (replica == null) {
                Debug.LogError("Prefab <i>" + prefab + "</i> is missing Replica Component", prefab);
                return null;
            }

            return newInstance;
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference reference) {
            return InstantiateReplicaAsync(reference.RuntimeKey, Vector3.zero, Quaternion.identity);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference reference, Vector3 position) {
            return InstantiateReplicaAsync(reference.RuntimeKey, position, Quaternion.identity);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference reference, Vector3 position, Quaternion rotation) {
            return InstantiateReplicaAsync(reference.RuntimeKey, position, rotation);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key) {
            return InstantiateReplicaAsync(key, Vector3.zero, Quaternion.identity);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position) {
            return InstantiateReplicaAsync(key, position, Quaternion.identity);
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position, Quaternion rotation) {
            var newInstance = Addressables.InstantiateAsync(key, position, rotation, spawnTransform);
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
            newReplica.Id = ReplicaId.Create(this);
            Assert.IsTrue(newReplica.Id != ReplicaId.Invalid);
            newReplica.TakeOwnership();

            // Wait for one frame until Start is called before replicating to clients
            replicasInConstruction[newReplica.Id] = newReplica;

            return newReplica;
        }

        /// <summary>
        /// Remove the Replica instantly from the manager, without notifying the clients.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        public void RemoveReplica(Replica replica) {
            if (replica.Id == ReplicaId.Invalid)
                return; // Replica already removed

            replicasInConstruction.Remove(replica.Id);
            networkScene.RemoveReplica(replica);

            if (replica.Id != ReplicaId.Invalid) {
                FreeLocalReplicaId(replica.Id);
                replica.Id = ReplicaId.Invalid;
            }
        }

        /// <summary>
        /// Removes the Replica instantly from this manager, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        public void DestroyReplica(Replica replica) {
            if (replica.Id == ReplicaId.Invalid)
                return; // Replica already destroyed

            // We don't need to send a destroy message if the replica is not yet fully constructed on the server
            bool alreadyConstructed = !replicasInConstruction.Remove(replica.Id);
            if (!alreadyConstructed) {
                networkScene.RemoveReplica(replica);
                replica.Id = ReplicaId.Invalid;
                UnityEngine.Object.Destroy(replica.gameObject);
                return;
            }

            replicasInDestruction.Add(replica);
        }

        public Replica GetReplica(ReplicaId id) {
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

                for (int i = 0; i < replicaViews.Count; ++i) {
                    var replicaView = replicaViews[i];
                    if (replicaView.IsLoadingLevel)
                        continue;

#if UNITY_EDITOR
                    TransportDebugger.BeginScope("Update " + replicaView.name);
#endif

                    UpdateReplicaView(replicaView);

#if UNITY_EDITOR
                    TransportDebugger.EndScope();
#endif
                }

                foreach (var replica in networkScene.Replicas) {
                    // Call RpcTarget.All RPCs on the server
                    // This is done in case the RPC destroys the Replica the RPC had a chance to be send
                    for (int i = 0; i < replica.queuedRpcs.Count; ++i) {
                        var queuedRpc = replica.queuedRpcs[i];
                        if (queuedRpc.target == RpcTarget.All) {
                            var _ = queuedRpc.bs.ReadByte();
                            var _2 = queuedRpc.bs.ReadReplicaId();
                            replica.CallRpcServer(Connection.Invalid, queuedRpc.bs);
                        }
                    }

                    replica.queuedRpcs.Clear();
                }

                foreach (var idReplicaPair in replicasInConstruction) {
                    var replica = idReplicaPair.Value;
                    networkScene.AddReplica(replica);

                    // Don't wait for relevant Replica set update, takes far too long for a newly spawned Replica
                    for (int i = 0; i < replicaViews.Count; ++i) {
                        var replicaView = replicaViews[i];
                        if (replicaView.IsLoadingLevel)
                            continue;

                        if (!IsReplicaRelevantForView(replica, replicaView))
                            continue;

                        var relevance = replica.GetRelevance(replicaView);
                        replicaView.RelevantReplicas.Add(replica);
                        replicaView.RelevantReplicaPriorityAccumulator.Add(relevance * relevance * 2); // 2 to boost sending new Replicas
                    }

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
                        FreeLocalReplicaId(replica.Id);

                        networkScene.RemoveReplica(replica);
                        replica.Id = ReplicaId.Invalid;
                        UnityEngine.Object.Destroy(replica.gameObject);
                    }
                } finally {
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

            var replicaIdRecycleTime = 5;
            nextReplicaIdRecycleTime = Time.time + replicaIdRecycleTime;
        }


        void UpdateReplicaView(ReplicaView view) {
            UpdateRelevantReplicaPriorities(view);

            int bytesSent = 0;

            List<int> sortedIndices = new List<int>();
            CalculateRelevantReplicaIndices(view, sortedIndices);

            var serializeCtx = new ReplicaBehaviour.SerializeContext() {
                View = view
            };

            int numSentLowRelevance = 0;
            foreach (var currentReplicaIdx in sortedIndices) {
                if (view.RelevantReplicaPriorityAccumulator[currentReplicaIdx] < 1)
                    continue; // < 1 means we are still not over the DesiredUpdateRate interval

                var replica = view.RelevantReplicas[currentReplicaIdx];
                if (replica == null || replica.Id == ReplicaId.Invalid)
                    continue;

                var relevance = replica.GetRelevance(view); // #todo COSTLY
                if (relevance <= settings.LowRelevance) {
                    if (numSentLowRelevance > settings.MaxLowRelevancePerPacket)
                        continue;

                    ++numSentLowRelevance;
                }

#if UNITY_EDITOR
                TransportDebugger.BeginScope("Update Replica " + replica.name);
#endif

                var updateBs = new BitStream();
                updateBs.Write((byte)MessageId.ReplicaUpdate);
                updateBs.Write(replica.isSceneReplica);
                if (!replica.isSceneReplica) {
                    updateBs.Write(replica.prefabIdx);
                }

                updateBs.Write(replica.Id);

                bool isOwner = replica.Owner == view.Connection;
                updateBs.Write(isOwner);

                replica.Serialize(updateBs, serializeCtx);

#if UNITY_EDITOR
                TransportDebugger.EndScope(updateBs.LengthInBits);
#endif

                server.NetworkInterface.Send(updateBs, PacketReliability.Unreliable, view.Connection);

                // We just sent this Replica, reset its priority
                view.RelevantReplicaPriorityAccumulator[currentReplicaIdx] = 0;

                bytesSent += updateBs.Length;
                if (bytesSent >= settings.MaxBytesPerConnectionPerUpdate)
                    break; // Packet size exhausted
            }

            // Rpcs
            foreach (var currentReplicaIdx in sortedIndices) {
                var replica = view.RelevantReplicas[currentReplicaIdx];
                if (replica == null || replica.Id == ReplicaId.Invalid)
                    continue;

                foreach (var queuedRpc in replica.queuedRpcs) {
                    var isRpcRelevant = (queuedRpc.target == RpcTarget.Owner && replica.Owner == view.Connection)
                        || queuedRpc.target == RpcTarget.All
                        || queuedRpc.target == RpcTarget.AllClients
                        || (queuedRpc.target == RpcTarget.AllClientsExceptOwner && replica.Owner != view.Connection);
                    if (!isRpcRelevant)
                        continue;
#if UNITY_EDITOR
                    TransportDebugger.BeginScope("Replica RPC " + queuedRpc.target);
#endif

                    server.NetworkInterface.Send(queuedRpc.bs, PacketReliability.Unreliable, view.Connection);

#if UNITY_EDITOR
                    TransportDebugger.EndScope(queuedRpc.bs.LengthInBits);
#endif

                    bytesSent += queuedRpc.bs.Length;
                    if (bytesSent >= settings.MaxBytesPerConnectionPerUpdate)
                        break; // Packet size exhausted
                }
            }
        }

        /// <summary>
        /// Build a list of relevant Replicas for this ReplicaView.
        /// </summary>
        void UpdateRelevantReplicas(ReplicaView view) {
            var oldAccs = new Dictionary<Replica, float>();
            for (int i = 0; i < view.RelevantReplicas.Count; ++i) {
                var replica = view.RelevantReplicas[i];
                if (replica == null)
                    continue;

                oldAccs.Add(replica, view.RelevantReplicaPriorityAccumulator[i]);
            }

            GatherRelevantReplicas(networkScene.Replicas, view, view.RelevantReplicas);

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

                if (!IsReplicaRelevantForView(replica, view))
                    continue;

                relevantReplicas.Add(replica);
            }
        }

        bool IsReplicaRelevantForView(Replica replica, ReplicaView view) {
            if (!replica.IsRelevantFor(view))
                return false;

            var relevance = replica.GetRelevance(view);
            if (relevance < settings.MinRelevance)
                return false;

            return true;
        }

        void UpdateRelevantReplicaPriorities(ReplicaView view) {
            // This function is called with a rate of settings.replicaUpdateRateMS

            for (int i = 0; i < view.RelevantReplicas.Count; ++i) {
                var replica = view.RelevantReplicas[i];
                if (replica == null)
                    continue;

                var relevance = replica.GetRelevance(view);
                Assert.IsTrue(relevance >= 0 && relevance <= 1);

                var a = (settings.ReplicaUpdateRateMS / replica.settings.DesiredUpdateRateMS) * (relevance * relevance);
                view.RelevantReplicaPriorityAccumulator[i] += a;
            }
        }

        static void CalculateRelevantReplicaIndices(ReplicaView view, List<int> sortedReplicaIndices) {
            sortedReplicaIndices.Clear();

            for (int i = 0; i < view.RelevantReplicas.Count; ++i) {
                sortedReplicaIndices.Add(i);
            }
            sortedReplicaIndices.Sort((i1, i2) => (int)((view.RelevantReplicaPriorityAccumulator[i2] - view.RelevantReplicaPriorityAccumulator[i1]) * 100));
        }

        void SendDestroyedReplicasToReplicaView(ReplicaView view) {
            Assert.IsTrue(replicasInDestruction.Count > 0);

            var destroyBs = BitStreamPool.Create();
            destroyBs.Write((byte)MessageId.ReplicaDestroy);

            var send = false;
            foreach (var replica in replicasInDestruction) {
                var wasInterestedInReplica = view.RelevantReplicas.Contains(replica);
                if (!wasInterestedInReplica)
                    continue;

#if UNITY_EDITOR
                TransportDebugger.BeginScope("ReplicaDestroy");
#endif
                destroyBs.Write(replica.Id);
                send = true;
#if UNITY_EDITOR
                TransportDebugger.EndScope(destroyBs.LengthInBits);
#endif
            }

            if (send) {
                server.NetworkInterface.Send(destroyBs, PacketReliability.Unreliable, view.Connection);
            }

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
            if (id.Data >= nextLocalReplicaId)
                return; // Tried to free id after Reset() was called

            replicaIdRecycleQueue.Enqueue(id.Data);
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

            replica.CallRpcServer(connection, bs);
        }
    }
}