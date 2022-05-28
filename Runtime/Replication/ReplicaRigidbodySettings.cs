using UnityEngine;

namespace Cube.Replication {
    [CreateAssetMenu(menuName = "Cube/ReplicaRigidbodySettings")]
    public class ReplicaRigidbodySettings : ScriptableObject {
        public float MaxVelocity = 100;
        public float MaxAngularVelocity = 10;
    }
}