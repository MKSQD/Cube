using System;
using UnityEngine;

namespace Cube.Transport {
    [Serializable]
    public struct ClientSimulatedLagSettings {
#if UNITY_EDITOR
        public bool enabled;
        [Range(0, 1)]
        public float simulatedLoss;
        [Range(0, 1)]
        public float duplicatesChance;
        [Range(0, 2)]
        public float minimumLatencySec;
        [Range(0, 1)]
        public float randomLatencySec;
#endif
    }
}