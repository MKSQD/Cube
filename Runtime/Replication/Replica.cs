using Cube.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;


namespace Cube.Replication {
    [AddComponentMenu("Cube/Replica")]
    [DisallowMultipleComponent]
    public class Replica : MonoBehaviour {
        public struct QueuedRpc {
            public RpcTarget target;
            public BitWriter bs;
        }

        public static ReplicaSettings DefaultReplicaSettings;
        public ReplicaSettings Settings;
        public ReplicaSettings SettingsOrDefault => Settings != null ? Settings : DefaultReplicaSettings;

        [HideInInspector]
        public ReplicaId Id = ReplicaId.Invalid;

        [HideInInspector]
        public ushort prefabIdx;
        [HideInInspector]
        public byte sceneIdx;

        public bool isSceneReplica => sceneIdx != 0;

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

        ReplicaBehaviour[] replicaBehaviours;
#if UNITY_EDITOR
        string[] replicaBehavioursNames;
#endif

        /// <summary>
        /// Used on the client to remove Replicas which received no updates for a long time.
        /// </summary>
        [HideInInspector]
        public float lastUpdateTime;

        public List<QueuedRpc> queuedRpcs = new List<QueuedRpc>();

        static bool applicationQuitting;

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

        /// [0,1]
        public virtual float GetRelevance(ReplicaView view) {
            if (!isSceneReplica && Owner == view.Connection)
                return 1;

            if ((Settings.priorityFlags & ReplicaPriorityFlag.IgnorePosition) == ReplicaPriorityFlag.IgnorePosition)
                return 1;

            var diff = new Vector2(transform.position.x - view.transform.position.x,
                transform.position.z - view.transform.position.z);

            var sqrMagnitude = diff.sqrMagnitude;
            if (sqrMagnitude > Settings.SqrMaxViewDistance)
                return 0; // No costly calculations

            var distanceRelevance = 1f - Mathf.Pow(sqrMagnitude / Settings.SqrMaxViewDistance, 0.9f);

            var viewForward = new Vector2(view.transform.forward.x, view.transform.forward.z).normalized;
            var dotRelevance = Vector2.Dot(viewForward, diff.normalized);
            dotRelevance = dotRelevance > 0 ? 1 : 0.5f;

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

        public void Serialize(BitWriter bs, ReplicaBehaviour.SerializeContext ctx) {
            for (int i = 0; i < replicaBehaviours.Length; ++i) {
                var replicaBehaviour = replicaBehaviours[i];
#if UNITY_EDITOR
                TransportDebugger.BeginScope(replicaBehavioursNames[i]);
                var startSize = bs.BitsWritten;
#endif

                replicaBehaviour.Serialize(bs, ctx);

#if UNITY_EDITOR || DEVELOPMENT
                bs.WriteByte((byte)0b10101010);
#endif

#if UNITY_EDITOR
                TransportDebugger.EndScope(bs.BitsWritten - startSize);
#endif
            }
        }

        public void Deserialize(BitReader bs) {
            foreach (var component in replicaBehaviours) {
                component.Deserialize(bs);

#if UNITY_EDITOR || DEVELOPMENT
                try {
                    if (bs.WouldReadPastEnd(8)) {
                        Debug.LogError($"{component} violated serialization guard (exhausted)");
                        return;
                    }

                    if (bs.ReadByte() != 0b10101010) {
                        Debug.LogError($"{component} violated serialization guard (invalid guard byte)");
                        return;
                    }
                } catch (InvalidOperationException) {
                    Debug.LogError($"{component} violated serialization guard (exception)");
                    return;
                }
#endif
            }
        }

        public void RebuildCaches() {
            // #todo This is disgusting
            var brbs = GetComponentsInChildren<BaseReplicaBehaviour>();

            var rbs = new List<ReplicaBehaviour>();
#if UNITY_EDITOR
            var rbNames = new List<string>();
#endif

            byte idx = 0;
            foreach (var brb in brbs) {
                brb.Replica = this;

                if (brb is ReplicaBehaviour rb) {
                    rb.replicaComponentIdx = idx++;
                    rbs.Add(rb);
#if UNITY_EDITOR
                    rbNames.Add(rb.ToString());
#endif
                }
            }

            replicaBehaviours = rbs.ToArray();
#if UNITY_EDITOR
            replicaBehavioursNames = rbNames.ToArray();
#endif
        }

        void Awake() {
            if (Settings == null) {
                if (DefaultReplicaSettings == null) {
                    DefaultReplicaSettings = ScriptableObject.CreateInstance<ReplicaSettings>();
                }

                Settings = DefaultReplicaSettings;
            }

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

            ReplicaBehaviour.rpcConnection = connection;
            try {
                var componentIdx = bs.ReadByte();
                var methodId = bs.ReadByte();

                var replicaBehaviour = replicaBehaviours[componentIdx];
                replicaBehaviour.DispatchRpc(methodId, bs);
            } finally {
                ReplicaBehaviour.rpcConnection = Connection.Invalid;
            }
        }

        public void CallRpcClient(BitReader bs) {
            var componentIdx = bs.ReadByte();
            var methodId = bs.ReadByte();

            var replicaBehaviour = replicaBehaviours[componentIdx];
            replicaBehaviour.DispatchRpc(methodId, bs);
        }
    }
}
