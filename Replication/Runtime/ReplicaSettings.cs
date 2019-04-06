using System;
using UnityEngine;

namespace Cube.Replication {
    [Flags]
    public enum ReplicaPriorityFlag {
        None = 0,
        IgnorePosition = 1
    }

    [CreateAssetMenu(menuName = "Cube.Networking.Replicas/ReplicaSettings")]
    public class ReplicaSettings : ScriptableObject {
        [Tooltip("How often updates should be send about this object to clients. 0 means as fast as possible.")]
        [Range(0, 2000)]
        public int desiredUpdateRateMs = Constants.replicaUpdateRateMS;
        public ReplicaPriorityFlag priorityFlags = ReplicaPriorityFlag.None;
        [Range(1, 1000)]
        public float maxViewDistance = 100;
    }
}