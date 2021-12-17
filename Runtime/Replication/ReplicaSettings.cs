using System;
using UnityEngine;

namespace Cube.Replication {
    [Flags]
    public enum ReplicaPriorityFlag {
        None = 0,
        IgnorePosition = 1
    }

    [CreateAssetMenu(menuName = "Cube/ReplicaSettings")]
    public class ReplicaSettings : ScriptableObject {
        [Tooltip("Desired update rate for Replicas. The Replica will never receive updates more often than this but is usually updated less often due to prioritisation and bandwidth.")]
        [Range(0, 2048)]
        public int DesiredUpdateRateMS = 200;
        public float DesiredUpdateRate => DesiredUpdateRateMS * 0.001f;

        public ReplicaPriorityFlag priorityFlags = ReplicaPriorityFlag.None;

        [Tooltip("Used for spacial prioritisation. Replicas further away than this distance are never send.")]
        [Range(1, 1000)]
        public float MaxViewDistance = 100;

        public float SqrMaxViewDistance => Mathf.Pow(MaxViewDistance, 2);
    }
}