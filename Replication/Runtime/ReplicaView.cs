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

        /// Ignore Replica positions when calculating relevancy. F.i. RTS game.
        public bool IgnoreReplicaPositionsForPriority = false;

        /// If true this ReplicaView is ignored. Will be set automatically.
        public bool IsLoadingLevel;

        [HideInInspector]
        public double NextPriorityUpdateTime;

        public List<Replica> RelevantReplicas;
        public List<float> RelevantReplicaPriorityAccumulator;

#if UNITY_EDITOR
        /// The ReplicaView currently being debugged.
        public static ReplicaView Debug;

        void OnDrawGizmosSelected() {
            Debug = this;
        }
#endif
    }
}
