using UnityEngine;

namespace Cube.Replication {
    [CreateAssetMenu(menuName = "Cube/ReplicaRigidbodySettings")]
    public class ReplicaRigidbodySettings : ScriptableObject {
        public float TeleportDistanceSqr = 1;

        public float MaxVelocity = 64;
        public float VelocityPrecision = 0.1f;

        public float MaxAngularVelocity = 10;
        public float AngularVelocityPrecision = 0.1f;
    }
}