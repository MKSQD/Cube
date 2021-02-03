using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cube.Replication {
    [Flags]
    public enum ReplicaPriorityFlag {
        None = 0,
        IgnorePosition = 1
    }

    [CreateAssetMenu(menuName = "Cube/ReplicaSettings")]
    public class ReplicaSettings : ScriptableObject {
        [FormerlySerializedAs("desiredUpdateRateMs")]
        [Tooltip("How often updates should be send about this object to clients. 0 means as fast as possible.")]
        [Range(0, 2048)]
        public int DesiredUpdateRateMS = 200;

        public ReplicaPriorityFlag priorityFlags = ReplicaPriorityFlag.None;

        [FormerlySerializedAs("maxViewDistance")]
        [Range(1, 1000)]
        public float MaxViewDistance = 300;
    }
}