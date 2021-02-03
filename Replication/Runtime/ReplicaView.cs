using UnityEngine;
using System;
using Cube.Transport;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Cube.Replication {
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
        [HideInInspector]
        public Connection Connection;
        [FormerlySerializedAs("ignoreReplicaPositionsForPriority")]
        public bool IgnoreReplicaPositionsForPriority = false;

        /// <summary>
        /// If true this ReplicaView is ignored. Will be set automatically.
        /// </summary>
        public bool IsLoadingLevel;

        public double NextPriorityUpdateTime;
        public List<Replica> RelevantReplicas;
        public List<float> RelevantReplicaPriorityAccumulator;

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
