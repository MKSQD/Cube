using UnityEngine;

namespace Cube.Replication {
    [CreateAssetMenu(menuName = "Cube/WorldBoundsSettings")]
    public class WorldBoundsSettings : ScriptableObject {
        public float MinWorldX = -10000, MaxWorldX = 10000;
        public float MinWorldY = -10000, MaxWorldY = 10000;
        public float MinWorldZ = -10000, MaxWorldZ = 10000;
    }
}