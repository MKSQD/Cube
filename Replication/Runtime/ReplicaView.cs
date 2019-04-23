using UnityEngine;
using System;
using Cube.Transport;
using System.Collections.Generic;

namespace Cube.Replication {
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
#if SERVER
        [HideInInspector]
        public Connection connection;
        public bool ignoreReplicaPositionsForPriority = false;

        /// <summary>
        /// If true this ReplicaView is ignored. Will be set automatically.
        /// </summary>
        public bool isLoadingLevel;

#if UNITY_EDITOR
        /// <summary>
        /// The view currently being debugged.
        /// </summary>
        public static ReplicaView debug;
#endif

        public List<Replica> relevantReplicas;
        public List<float> relevantReplicaPriorityAccumulator;

        void OnDrawGizmosSelected() {
            debug = this;
        }
#endif
    }
}
