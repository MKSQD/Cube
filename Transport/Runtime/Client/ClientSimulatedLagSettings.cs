using System;
using UnityEngine;

namespace Cube.Networking.Transport {
    [Serializable]
    public struct ClientSimulatedLagSettings {
#if UNITY_EDITOR
        public bool enabled;
        [Range(0, 1)]
        public float simulatedLoss;
        [Range(0, 1)]
        public float duplicatesChance;
        [Range(0, 1000)]
        public float minimumLatencyMs;
        [Range(0, 1000)]
        public float randomLatencyMs;
#endif
    }
}