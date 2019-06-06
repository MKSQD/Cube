using UnityEngine;
using System;
using Cube.Transport;
using System.Collections.Generic;

namespace Cube.Replication {
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
        [HideInInspector]
        public Connection connection;
        public bool ignoreReplicaPositionsForPriority = false;

        /// <summary>
        /// If true this ReplicaView is ignored. Will be set automatically.
        /// </summary>
        public bool isLoadingLevel;
        
        public List<Replica> relevantReplicas;
        public List<float> relevantReplicaPriorityAccumulator;

#if UNITY_EDITOR
        /// <summary>
        /// The view currently being debugged.
        /// </summary>
        public static ReplicaView debug;

        void OnDrawGizmosSelected() {
            debug = this;
        }
#endif
    }
}
