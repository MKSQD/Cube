using System;
using System.Collections.Generic;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;


namespace Cube.Replication {
    [AddComponentMenu("Cube/Replica")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class Replica : MonoBehaviour {
        public struct QueuedRpc {
            public RpcTarget target;
            public BitWriter bs;
        }

        public static ReplicaSettings DefaultSettings;
        public ReplicaSettings Settings;
        public ReplicaSettings SettingsOrDefault => Settings != null ? Settings : DefaultSettings;

        [HideInInspector]
        public ReplicaId Id = ReplicaId.Invalid;

        [HideInInspector]
        public ushort PrefabHash;

        /// <summary>
        /// Preallocated Replica ID (f.i. level Replicas). If set Cube will assume
        /// that client and server have this and will not attempt to instantiate it if missing.
        /// </summary>
        [HideInInspector]
        public ushort StaticId;
        public bool HasStaticId => StaticId != 0;

        public ICubeServer server;
        public ICubeClient client;
        public IReplicaManager ReplicaManager => server != null ? (IReplicaManager)server.ReplicaManager : client.ReplicaManager;

        public bool isServer => server != null;
        public bool isClient => client != null;

        /// <summary>
        /// The connection (client) owning this Replica, or Connection.Invalid if the server owns it.
        /// Only valid on the server.
        /// </summary>
        public Connection Owner { get; private set; }
        public bool IsOwner { get; private set; }

        /// <summary>
        /// Used on the client to remove Replicas which received no updates for a long time.
        /// </summary>
        [HideInInspector]
        public float lastUpdateTime;

        public List<QueuedRpc> queuedRpcs = new();

        static bool applicationQuitting;

        [HideInInspector]
        [SerializeField]
        ReplicaBehaviour[] _replicaBehaviours;

        public void AssignOwnership(Connection owner) {
            Assert.IsTrue(isServer);
            Assert.IsTrue(owner != Connection.Invalid);

            Owner = owner;
            IsOwner = false;
        }

        public void TakeOwnership() {
            Assert.IsTrue(isServer);

            Owner = Connection.Invalid;
            IsOwner = true;
        }

        public void ClientUpdateOwnership(bool owned) {
            Assert.IsTrue(owned != IsOwner);
            IsOwner = owned;
        }

        public bool IsRelevantFor(ReplicaView view) {
            Assert.IsTrue(isServer);
            return gameObject.activeInHierarchy;
        }

        // [0,1]
        public float GetRelevance(ReplicaView view) {
            if (!HasStaticId && Owner == view.Connection)
                return 1;

            if ((Settings.priorityFlags & ReplicaPriorityFlag.IgnorePosition) == ReplicaPriorityFlag.IgnorePosition)
                return 1;

            var diff = new Vector2(transform.position.x - view.transform.position.x,
                transform.position.z - view.transform.position.z);

            var sqrMagnitude = diff.sqrMagnitude;
            if (sqrMagnitude > Settings.SqrMaxViewDistance)
                return 0; // No costly calculations

            var distanceRelevance = 1f - Mathf.Pow(sqrMagnitude / Settings.SqrMaxViewDistance, 0.8f);

            var viewForward = new Vector2(view.transform.forward.x, view.transform.forward.z).normalized;
            var dotRelevance = Vector2.Dot(viewForward, diff.normalized);
            dotRelevance = dotRelevance > 0 ? 1 : 0.8f;

            return distanceRelevance * dotRelevance;
        }

        /// <summary>
        /// SERVER only.
        /// Removes the Replica instantly from replication, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// </summary>
        public void Destroy() {
            if (!isServer)
                return;

            server.ReplicaManager.DestroyReplica(this);
        }

        /// <summary>
        /// SERVER only. 
        /// Removes the Replica instantly from replication.
        /// Does NOT send any message to the clients.
        /// Does NOT destroy the gameObject.
        /// </summary>
        public void Remove() {
            if (!isServer)
                return;

            server.ReplicaManager.RemoveReplica(this);
        }

        public void Serialize(IBitWriter bs, ReplicaBehaviour.SerializeContext ctx) {
            for (int i = 0; i < _replicaBehaviours.Length; ++i) {
                var replicaBehaviour = _replicaBehaviours[i];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                bs.WriteInt(replicaBehaviour.GetType().GetHashCode());
#endif

#if UNITY_EDITOR
                int startSize = 0;
                var isDummy = bs is DummyBitWriter;
                if (!isDummy) {
                    TransportDebugger.BeginScope(replicaBehaviour.GetType().Name);
                    startSize = bs.BitsWritten;
                }
#endif

                replicaBehaviour.Serialize(bs, ctx);

#if UNITY_EDITOR
                if (!isDummy) {
                    TransportDebugger.EndScope(bs.BitsWritten - startSize);
                }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                bs.WriteByte(0b10101010);
#endif
            }
        }

        public void Deserialize(BitReader bs) {
            foreach (var replicaBehaviour in _replicaBehaviours) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var expectedComponentTypeHash = bs.ReadInt();
                if (expectedComponentTypeHash != replicaBehaviour.GetType().GetHashCode()) {
                    Debug.LogError($"{replicaBehaviour} not the expected component type", gameObject);
                    return;
                }
#endif

                replicaBehaviour.Deserialize(bs);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                try {
                    if (bs.WouldReadPastEnd(8)) {
                        Debug.LogError($"{replicaBehaviour} violated serialization guard (exhausted)", gameObject);
                        return;
                    }

                    if (bs.ReadByte() != 0b10101010) {
                        Debug.LogError($"{replicaBehaviour} violated serialization guard (invalid guard byte)", gameObject);
                        return;
                    }
                } catch (InvalidOperationException) {
                    Debug.LogError($"{replicaBehaviour} violated serialization guard (exception)", gameObject);
                    return;
                }
#endif
            }
        }

        public void RebuildCaches() {
            var brbs = GetComponentsInChildren<BaseReplicaBehaviour>();

            var rbs = new List<ReplicaBehaviour>(brbs.Length);

            byte idx = 0;
            foreach (var brb in brbs) {
                brb.Replica = this;

                if (brb is ReplicaBehaviour rb) {
                    rb.ReplicaComponentIdx = idx++;
                    rbs.Add(rb);
                }
            }

            _replicaBehaviours = rbs.ToArray();
        }

        void Awake() {
            if (Settings == null) {
                if (DefaultSettings == null) {
                    DefaultSettings = ScriptableObject.CreateInstance<ReplicaSettings>();
                }
                Settings = DefaultSettings;
            }

            //RebuildCaches();
        }

        void OnValidate() {
            RebuildCaches();
        }

        /// <summary>
        /// Removes the Replica from all global managers. Does NOT broadcast its destruction.
        /// </summary>
        void OnDestroy() {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return; // No need to remove this if not in play mode
#endif
            if (applicationQuitting)
                return;

            if (isClient) {
                client.ReplicaManager.RemoveReplica(this);
            }
            if (isServer) {
                server.ReplicaManager.RemoveReplica(this);
            }
        }

        void OnApplicationQuit() {
            applicationQuitting = true;
        }

        public void QueueServerRpc(BitWriter bs, RpcTarget target) {
            bs.FlushBits();

            var qrpc = new QueuedRpc() {
                bs = bs,
                target = target
            };
            queuedRpcs.Add(qrpc);
        }

        public void CallRpcServer(Connection connection, BitReader bs) {
            if (connection != Connection.Invalid) {
                var isReplicaOwnedByCaller = Owner == connection;
                if (!isReplicaOwnedByCaller) {
#if CUBE_DEBUG_REP
                    Debug.LogWarning($"Replica RPC called by non-owner, rejected (Replica={gameObject})", gameObject);
#endif
                    return;
                }
            }

            ReplicaBehaviour.RpcConnection = connection;
            try {
                var componentIdx = bs.ReadByte();
                var methodId = bs.ReadByte();

                var replicaBehaviour = _replicaBehaviours[componentIdx];
                replicaBehaviour.DispatchRpc(methodId, bs);
            } finally {
                ReplicaBehaviour.RpcConnection = Connection.Invalid;
            }
        }

        public void CallRpcClient(BitReader bs) {
            var componentIdx = bs.ReadByte();
            var methodId = bs.ReadByte();

            var replicaBehaviour = _replicaBehaviours[componentIdx];
            replicaBehaviour.DispatchRpc(methodId, bs);
        }
    }
}
