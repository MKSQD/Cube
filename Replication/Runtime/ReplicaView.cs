using UnityEngine;
using System;
using Cube.Transport;
using System.Collections.Generic;
using UnityEditor;

namespace Cube.Replication {
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
        [HideInInspector]
        public Connection Connection;

        /// <summary>
        /// If true this ReplicaView is ignored. Will be set automatically.
        /// </summary>
        public bool IsLoadingLevel;

        [HideInInspector]
        public double NextPriorityUpdateTime;


        // These Lists are in sync
        public int NumRelevantReplicas = 0;
        public List<Replica> RelevantReplicas = new List<Replica>();
        public List<float> RelevantReplicaPriorityAccumulator = new List<float>();

#if UNITY_EDITOR
        /// <summary>
        /// The view currently being debugged.
        /// </summary>
        public static ReplicaView Debug;

        void OnDrawGizmosSelected() {
            Debug = this;

            Handles.color = Color.white;
            for (int i = 1; i < 3; ++i) {
                Handles.DrawWireDisc(transform.position, Vector3.up, i * 50);
            }
        }
#endif
    }
}
