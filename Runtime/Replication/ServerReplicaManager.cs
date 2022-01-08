using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;


namespace Cube.Replication {
    public sealed class ServerReplicaManager : IServerReplicaManager {
#if UNITY_EDITOR
        public static ServerReplicaManager Main;
#endif

        const ushort FirstLocalReplicaId = 255; // The first 255 values are reserved for scene Replicas

        readonly ICubeServer _server;
        readonly NetworkScene _networkScene;
        public ReadOnlyCollection<Replica> Replicas => _networkScene.Replicas;

        [SerializeField]
        List<ReplicaView> _replicaViews = new List<ReplicaView>();
        public List<ReplicaView> ReplicaViews => _replicaViews;

        ushort _nextLocalReplicaId = FirstLocalReplicaId;
        readonly Queue<ushort> _freeReplicaIds = new Queue<ushort>();

        float _nextReplicaIdRecycleTime = 0;
        readonly Queue<ushort> _replicaIdRecycleQueue = new Queue<ushort>();

        Dictionary<ReplicaId, Replica> _replicasInConstruction = new Dictionary<ReplicaId, Replica>();
        List<Replica> _replicasInDestruction = new List<Replica>();

        public ServerReplicaManager(ICubeServer server) {
            Assert.IsNotNull(server);

            _networkScene = new NetworkScene();

            this._server = server;
            server.Reactor.AddHandler((byte)MessageId.ReplicaRpc, OnReplicaRpc);

            SceneManager.sceneLoaded += (scene, mode) => ProcessSceneReplicasInScene(scene);

#if UNITY_EDITOR
            Main = this;
#endif
        }

        /// Scan a newly loaded Scene for scene Replicas.
        public void ProcessSceneReplicasInScene(Scene scene) {
            var sceneReplicas = ReplicaUtils.GatherSceneReplicas(scene);
            foreach (var replica in sceneReplicas) {
                replica.Id = ReplicaId.CreateFromExisting(replica.sceneIdx);
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

        public GameObject InstantiateReplica(GameObject prefab) => InstantiateReplica(prefab, Vector3.zero, Quaternion.identity);
        public GameObject InstantiateReplica(GameObject prefab, Vector3 position) => InstantiateReplica(prefab, position, Quaternion.identity);

        public GameObject InstantiateReplica(GameObject prefab, Vector3 position, Quaternion rotation) {
            if (prefab == null)
                throw new ArgumentNullException("prefab");

            var newInstance = UnityEngine.Object.Instantiate(prefab, position, rotation, _server.ReplicaParentTransform);
            var replica = InstantiateReplicaImpl(newInstance);
            if (replica == null) {
                Debug.LogError($"Prefab <i>{prefab}</i> is missing Replica Component", prefab);
                return null;
            }

            return newInstance;
        }

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference reference) => InstantiateReplicaAsync(reference.RuntimeKey, Vector3.zero, Quaternion.identity);
        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference reference, Vector3 position) => InstantiateReplicaAsync(reference.RuntimeKey, position, Quaternion.identity);
        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(AssetReference reference, Vector3 position, Quaternion rotation) => InstantiateReplicaAsync(reference.RuntimeKey, position, rotation);
        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key) => InstantiateReplicaAsync(key, Vector3.zero, Quaternion.identity);
        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position) => InstantiateReplicaAsync(key, position, Quaternion.identity);

        public AsyncOperationHandle<GameObject> InstantiateReplicaAsync(object key, Vector3 position, Quaternion rotation) {
            var newInstance = Addressables.InstantiateAsync(key, position, rotation, _server.ReplicaParentTransform);
            newInstance.Completed += obj => {
                if (obj.Result == null) {
                    Debug.LogError($"Instaniate failed, maybe invalid key '{key}'");
                    return;
                }

                var replica = InstantiateReplicaImpl(obj.Result);
                if (replica == null) {
                    Debug.LogError($"Prefab <i>{key}</i> is missing Replica Component");
                }
            };

            return newInstance;
        }

        Replica InstantiateReplicaImpl(GameObject newInstance) {
            var newReplica = newInstance.GetComponent<Replica>();
            if (newReplica == null)
                return null;

            newReplica.server = _server;
            newReplica.Id = ReplicaId.Create(this);
            Assert.IsTrue(newReplica.Id != ReplicaId.Invalid);
            newReplica.TakeOwnership();

            // Wait for one frame until Start is called before replicating to clients
            _replicasInConstruction[newReplica.Id] = newReplica;

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

            _replicasInConstruction.Remove(replica.Id);
            _networkScene.RemoveReplica(replica);

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
            bool alreadyConstructed = !_replicasInConstruction.Remove(replica.Id);
            if (!alreadyConstructed) {
                _networkScene.RemoveReplica(replica);
                replica.Id = ReplicaId.Invalid;
                UnityEngine.Object.Destroy(replica.gameObject);
                return;
            }

            _replicasInDestruction.Add(replica);
        }

        public Replica GetReplica(ReplicaId id) => _networkScene.GetReplicaById(id);

        public void Tick() {
            for (int i = 0; i < _replicaViews.Count; ++i) {
                var replicaView = _replicaViews[i];
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

            foreach (var replica in _networkScene.Replicas) {
                try {
                    // Call RpcTarget.All RPCs on the server
                    // This is done in case the RPC destroys the Replica the RPC had a chance to be send
                    for (int i = 0; i < replica.queuedRpcs.Count; ++i) {
                        var queuedRpc = replica.queuedRpcs[i];
                        if (queuedRpc.target == RpcTarget.All) {
                            var reader = new BitReader(queuedRpc.bs);

                            var _ = reader.ReadByte();
                            var _2 = reader.ReadReplicaId();
                            replica.CallRpcServer(Connection.Invalid, reader);
                        }
                    }
                } finally {
                    replica.queuedRpcs.Clear();
                }
            }

            foreach (var idReplicaPair in _replicasInConstruction) {
                var replica = idReplicaPair.Value;
                if (replica == null)
                    continue;

                _networkScene.AddReplica(replica);

                // Don't wait for relevant Replica set update, takes far too long for a newly spawned Replica
                for (int i = 0; i < _replicaViews.Count; ++i) {
                    var replicaView = _replicaViews[i];
                    if (replicaView.IsLoadingLevel)
                        continue;

                    if (!IsReplicaRelevantForView(replica, replicaView))
                        continue;

                    var relevance = replica.GetRelevance(replicaView);
                    replicaView.NumRelevantReplicas++;
                    replicaView.RelevantReplicas.Add(replica);
                    replicaView.RelevantReplicaPriorityAccumulator.Add(relevance * relevance * 2); // 2 to boost sending new Replicas
                }

            }
            _replicasInConstruction.Clear();

            // Actually destroy queued Replicas
            if (_replicasInDestruction.Count > 0) {
                try {
                    for (int i = 0; i < _replicaViews.Count; ++i) {
                        var replicaView = _replicaViews[i];
                        if (replicaView.IsLoadingLevel)
                            continue;

                        SendDestroyedReplicasToReplicaView(replicaView);
                    }

                    foreach (var replica in _replicasInDestruction) {
                        FreeLocalReplicaId(replica.Id);

                        _networkScene.RemoveReplica(replica);
                        replica.Id = ReplicaId.Invalid;
                        UnityEngine.Object.Destroy(replica.gameObject);
                    }
                } finally {
                    _replicasInDestruction.Clear();
                }
            }
        }

        public void Update() {
            RecycleReplicaIdsAfterDelay();

            // Update set of relevant Replicas
            for (int i = 0; i < _replicaViews.Count; ++i) {
                var replicaView = _replicaViews[i];
                if (replicaView.IsLoadingLevel)
                    continue;

                if (Time.timeAsDouble >= replicaView.NextPriorityUpdateTime) {
                    replicaView.NextPriorityUpdateTime = Time.timeAsDouble + _server.ReplicaManagerSettings.ReplicaRelevantSetUpdateRate;
                    UpdateRelevantReplicas(replicaView);
                }
            }
        }

        public void ForceReplicaViewRefresh(ReplicaView view) {
            Assert.IsTrue(!view.IsLoadingLevel);

            UpdateRelevantReplicas(view);
        }

        void RecycleReplicaIdsAfterDelay() {
            if (Time.time < _nextReplicaIdRecycleTime)
                return;

            if (_replicaIdRecycleQueue.Count > 0) {
                var id = _replicaIdRecycleQueue.Dequeue();
                _freeReplicaIds.Enqueue(id);
            }

            var replicaIdRecycleTime = 5;
            _nextReplicaIdRecycleTime = Time.time + replicaIdRecycleTime;
        }




        List<int> indicesSortedByPriority = new(512);
        void UpdateReplicaView(ReplicaView view) {
            // Clear dead Replicas so we dont have to check in following code
            {
                for (int i = 0; i < view.NumRelevantReplicas;) {
                    if (view.RelevantReplicas[i] != null) {
                        ++i;
                        continue;
                    }

                    --view.NumRelevantReplicas;
                    view.RelevantReplicas[i] = view.RelevantReplicas[view.NumRelevantReplicas];
                    view.RelevantReplicas.RemoveAt(view.NumRelevantReplicas);

                    view.RelevantReplicaPriorityAccumulator[i] = view.RelevantReplicaPriorityAccumulator[view.NumRelevantReplicas];
                    view.RelevantReplicaPriorityAccumulator.RemoveAt(view.NumRelevantReplicas);
                }

                Assert.AreEqual(view.RelevantReplicas.Count, view.NumRelevantReplicas);
                Assert.AreEqual(view.RelevantReplicas.Count, view.RelevantReplicaPriorityAccumulator.Count);
            }

            UpdateRelevantReplicaPriorities(view);
            CalculateRelevantReplicaIndices(view, indicesSortedByPriority);

            int bytesSent = 0;

            int numSentLowRelevance = 0;
            for (int i = 0; i < Math.Min(_server.ReplicaManagerSettings.MaxReplicasPerPacket, view.NumRelevantReplicas); ++i) {
                var currentReplicaIdx = indicesSortedByPriority[i];

                var priorityAcc = view.RelevantReplicaPriorityAccumulator[currentReplicaIdx];
                if (priorityAcc < 1)
                    break; // < 1 means we are still not over the DesiredUpdateRate interval

                var replica = view.RelevantReplicas[currentReplicaIdx];

                var relevance = replica.GetRelevance(view); // #todo COSTLY
                if (relevance <= _server.ReplicaManagerSettings.LowRelevance) {
                    if (numSentLowRelevance > _server.ReplicaManagerSettings.MaxLowRelevancePerPacket)
                        continue; //  #todo should be break

                    ++numSentLowRelevance;
                }



                var isOwner = replica.Owner == view.Connection;

                var dummyBs = new DummyBitWriter();
                WriteReplicaUpdate(dummyBs, replica, isOwner);
                if (bytesSent + dummyBs.BytesWritten > _server.ReplicaManagerSettings.MaxBytesPerConnectionPerUpdate) {
#if UNITY_EDITOR
                    TransportDebugger.BeginScope($"Skipped Replica {replica.name} priorityAcc={priorityAcc:0} bytes={dummyBs.BytesWritten}");
                    TransportDebugger.EndScope(0);
#endif

                    continue; // This Replica update would send too much data, try the next Replica
                }

#if UNITY_EDITOR
                TransportDebugger.BeginScope($"Update Replica {replica.name} priorityAcc={priorityAcc:0} bytes={dummyBs.BytesWritten}");
#endif


                var updateBs = new BitWriter(32);
                WriteReplicaUpdate(updateBs, replica, isOwner);
                updateBs.FlushBits();
#if UNITY_EDITOR
                TransportDebugger.EndScope(updateBs.BitsWritten);
#endif
                bytesSent += updateBs.BytesWritten;

                _server.NetworkInterface.Send(updateBs, PacketReliability.Unreliable, view.Connection, MessageChannel.State);

                // We just sent this Replica, reset its priority
                view.RelevantReplicaPriorityAccumulator[currentReplicaIdx] = 0;
            }

            // Rpcs
            foreach (var currentReplicaIdx in indicesSortedByPriority) {
                var replica = view.RelevantReplicas[currentReplicaIdx];
                foreach (var queuedRpc in replica.queuedRpcs) {
                    var isRpcRelevant = (queuedRpc.target == RpcTarget.Owner && replica.Owner == view.Connection)
                        || queuedRpc.target == RpcTarget.All
                        || queuedRpc.target == RpcTarget.AllClients
                        || (queuedRpc.target == RpcTarget.AllClientsExceptOwner && replica.Owner != view.Connection);
                    if (!isRpcRelevant)
                        continue;

                    if (bytesSent + queuedRpc.bs.BytesWritten >= _server.ReplicaManagerSettings.MaxBytesPerConnectionPerUpdate)
                        break; // Packet size exhausted

#if UNITY_EDITOR
                    TransportDebugger.BeginScope("Replica RPC " + queuedRpc.target);
#endif

                    _server.NetworkInterface.Send(queuedRpc.bs, PacketReliability.Unreliable, view.Connection, MessageChannel.Rpc);

#if UNITY_EDITOR
                    TransportDebugger.EndScope(queuedRpc.bs.BitsWritten);
#endif

                    bytesSent += queuedRpc.bs.BytesWritten;
                }
            }
        }

        void WriteReplicaUpdate(IBitWriter bs, Replica replica, bool isOwner) {
            bs.WriteByte((byte)MessageId.ReplicaUpdate);
            bs.WriteBool(replica.isSceneReplica);
            if (!replica.isSceneReplica) {
                bs.WriteUShort(replica.prefabIdx);
            }
            bs.WriteReplicaId(replica.Id);
            bs.WriteBool(isOwner);

            var serializeCtx = new ReplicaBehaviour.SerializeContext() {
                IsOwner = isOwner
            };
            replica.Serialize(bs, serializeCtx);
        }

        Dictionary<Replica, float> oldAccs = new();
        /// <summary>
        /// Build a list of relevant Replicas for this ReplicaView.
        /// </summary>
        void UpdateRelevantReplicas(ReplicaView view) {
            oldAccs.Clear();
            for (int i = 0; i < view.NumRelevantReplicas; ++i) {
                var replica = view.RelevantReplicas[i];
                oldAccs.Add(replica, view.RelevantReplicaPriorityAccumulator[i]);
            }

            GatherRelevantReplicas(_networkScene.Replicas, view, view.RelevantReplicas);
            view.NumRelevantReplicas = view.RelevantReplicas.Count;

            view.RelevantReplicaPriorityAccumulator.Clear();
            foreach (var replica in view.RelevantReplicas) {
                oldAccs.TryGetValue(replica, out float acc);
                view.RelevantReplicaPriorityAccumulator.Add(acc);
            }
        }

        void GatherRelevantReplicas(ReadOnlyCollection<Replica> replicas, ReplicaView view, List<Replica> relevantReplicas) {
            relevantReplicas.Clear();

            foreach (var replica in replicas) {
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
            if (relevance < _server.ReplicaManagerSettings.MinRelevance)
                return false;

            return true;
        }

        void UpdateRelevantReplicaPriorities(ReplicaView view) {
            // This function is called with a rate of settings.replicaUpdateRateMS

            for (int i = 0; i < view.NumRelevantReplicas; ++i) {
                var replica = view.RelevantReplicas[i];
                var relevance = replica.GetRelevance(view);

                var a = (_server.ReplicaManagerSettings.ReplicaUpdateRateMS / replica.Settings.DesiredUpdateRateMS) * (relevance * relevance);
                view.RelevantReplicaPriorityAccumulator[i] += a;
            }
        }

        static void CalculateRelevantReplicaIndices(ReplicaView view, List<int> list) {
            list.Clear();
            for (int i = 0; i < view.NumRelevantReplicas; ++i) {
                list.Add(i);
            }

            list.Sort((i1, i2) => (int)((view.RelevantReplicaPriorityAccumulator[i2] - view.RelevantReplicaPriorityAccumulator[i1]) * 100));
        }

        void SendDestroyedReplicasToReplicaView(ReplicaView view) {
            Assert.IsTrue(_replicasInDestruction.Count > 0);

            var destroyBs = new BitWriter();
            destroyBs.WriteByte((byte)MessageId.ReplicaDestroy);

            var send = false;
            foreach (var replica in _replicasInDestruction) {
                var wasInterestedInReplica = view.RelevantReplicas.Contains(replica);
                if (!wasInterestedInReplica)
                    continue;

#if UNITY_EDITOR
                TransportDebugger.BeginScope("ReplicaDestroy");
#endif
                destroyBs.WriteReplicaId(replica.Id);
                send = true;
#if UNITY_EDITOR
                TransportDebugger.EndScope(destroyBs.BitsWritten);
#endif
            }

            if (send) {
                _server.NetworkInterface.Send(destroyBs, PacketReliability.Unreliable, view.Connection, MessageChannel.State);
            }
        }

        public ReplicaView GetReplicaView(Connection connection) {
            foreach (var replicaView in _replicaViews) {
                if (replicaView.Connection == connection)
                    return replicaView;
            }
            return null;
        }

        public void AddReplicaView(ReplicaView view) {
            if (_replicaViews.Contains(view))
                return;

            if (view.Connection == Connection.Invalid)
                throw new ArgumentException("connection");

            _replicaViews.Add(view);
        }

        public void RemoveReplicaView(Connection connection) {
            for (int i = 0; i < _replicaViews.Count; ++i) {
                if (_replicaViews[i].Connection == connection) {
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
            if (id.Data >= _nextLocalReplicaId)
                return; // Tried to free id after Reset() was called

            _replicaIdRecycleQueue.Enqueue(id.Data);
        }

        void OnReplicaRpc(Connection connection, BitReader bs) {
            var replicaId = bs.ReadReplicaId();

            var replica = _networkScene.GetReplicaById(replicaId);
            if (replica == null) {
#if CUBE_DEBUG_REP
                Debug.LogWarning($"Received RPC for invalid Replica <i>{replicaId}</i>");
#endif
                return;
            }

            replica.CallRpcServer(connection, bs);
        }
    }
}