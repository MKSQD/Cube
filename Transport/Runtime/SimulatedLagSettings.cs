using System;
using UnityEngine;

namespace Cube.Transport {
    [Serializable]
    public struct SimulatedLagSettings {
        public bool enabled;
        [Range(0, 100)]
        public float simulatedLossPercent;
        [Range(0, 100)]
        public float duplicatesChancePercent;
        [Range(0, 500)]
        public float minimumLatencyMs;
        [Range(0, 500)]
        public float additionalRandomLatencyMs;
    }
}