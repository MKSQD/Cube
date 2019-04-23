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
        [Tooltip("How often updates should be send about this object to clients. 0 means as fast as possible.")]
        [Range(1, 2048)]
        public int desiredUpdateRateMs = 200;
        public ReplicaPriorityFlag priorityFlags = ReplicaPriorityFlag.None;
        [Range(1, 1000)]
        public float maxViewDistance = 100;
    }
}