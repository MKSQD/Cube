using UnityEngine;
using System;
using Cube.Transport;
using System.Collections.Generic;

namespace Cube.Replication {
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
        [HideInInspector]
        public Connection Connection;

        /// <summary>
        /// Ignore Replica positions when calculating relevancy. F.i. RTS game.
        /// </summary>
        public bool IgnoreReplicaPositionsForPriority = false;

        /// <summary>
        /// If true this ReplicaView is ignored. Will be set automatically.
        /// </summary>
        public bool IsLoadingLevel;

        [HideInInspector]
        public double NextPriorityUpdateTime;
        public List<Replica> RelevantReplicas = new List<Replica>();
        public List<float> RelevantReplicaPriorityAccumulator = new List<float>();

#if UNITY_EDITOR
        /// <summary>
        /// The view currently being debugged.
        /// </summary>
        public static ReplicaView Debug;

        void OnDrawGizmosSelected() {
            Debug = this;
        }
#endif
    }
}
