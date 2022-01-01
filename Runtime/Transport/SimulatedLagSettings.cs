using System;
using UnityEngine;

namespace Cube.Transport {
    [Serializable]
    public struct SimulatedLagSettings {
        public bool enabled;
        [Range(0, 100)]
        public int simulatedLossPercent;
        [Range(0, 100)]
        public int duplicatesChancePercent;
        [Range(0, 500)]
        public int minimumLatencyMs;
        [Range(0, 500)]
        public int additionalRandomLatencyMs;
    }
}