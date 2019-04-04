using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Networking.Replicas {
    [AddComponentMenu("Cube.Networking.Replicas/Replica")]
    [DisallowMultipleComponent]
    public sealed class Replica : NetworkBehaviour {
        public ReplicaSettings settings;

        [HideInInspector]
        public ReplicaId id = ReplicaId.Invalid;
        
        public ushort prefabIdx;
        public byte sceneIdx = byte.MaxValue;


        public ReplicaBehaviour[] replicaBehaviours {
            get;
            internal set;
        }
        
#if CLIENT
        [HideInInspector]
        public float lastUpdateTime;
#endif

        static bool _applicationQuitting;

        /// <summary>
        /// Removes the Replica instantly from the ReplicaManager, destroys the GameObject and sends a destroy message to the clients on the next update.
        /// @see ServerReplicaManager.DestroyReplica
        /// </summary>
        /// <param name="replica">The Replica to remove</param>
        public void Destroy() {
            Assert.IsTrue(isServer);
#if SERVER
            server.replicaManager.DestroyReplica(this);
#endif
        }

        public void RebuildReplicaBehaviourCache() {
            replicaBehaviours = GetComponentsInChildren<ReplicaBehaviour>();
            for (byte i = 0; i < replicaBehaviours.Length; ++i) {
                replicaBehaviours[i].replicaComponentIdx = i;
            }
        }

        void Awake() {
            RebuildReplicaBehaviourCache();
        }

        /// <summary>
        /// Removes the Replica from all global managers. Does NOT broadcast its destruction.
        /// </summary>
        void OnDestroy() {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return; // No need to remove this if not in play mode
#endif
            if (_applicationQuitting)
                return;
#if SERVER
            if (isServer)
                server.replicaManager.RemoveReplica(this);
#endif
#if CLIENT
            if (isClient)
                client.replicaManager.RemoveReplica(this);
#endif
        }

        void OnApplicationQuit() {
            _applicationQuitting = true;
        }
    }
}
