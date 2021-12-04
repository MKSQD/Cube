using System;
using UnityEngine;

namespace Cube.Replication {
    [Serializable]
    public class ServerReplicaManagerSettings {
        [Range(0, 1500)]
        public int MaxBytesPerConnectionPerUpdate = 1400;
        [Range(1000f / 60f, 1000f / 1f)]
        public float ReplicaUpdateRateMS = 33; // 30 times per second
        public float ReplicaUpdateRate {
            get { return ReplicaUpdateRateMS * 0.001f; }
        }

        [Range(1000f / 60f, 1000f / 0.1f)]
        public float ReplicaRelevantSetUpdateRateMS = 1000;
        public float ReplicaRelevantSetUpdateRate {
            get { return ReplicaRelevantSetUpdateRateMS * 0.001f; }
        }

        [Tooltip("Replicas below this relevance in relation to the ReplicaView will not be considered for replication")]
        [Range(0f, 1f)]
        public float MinRelevance = 0.15f;

        [Tooltip("Replicas below this relevance in relation to the ReplicaView will be limited from being included in a packet")]
        [Range(0f, 1f)]
        public float LowRelevance = 0.3f;

        [Tooltip("Max number of low relevance Replicas (see LowRelevance) that can be included in a packet")]
        [Range(1, 40)]
        public int MaxLowRelevancePerPacket = 3;

        public int MaxReplicasPerPacket = 20;
    }
}