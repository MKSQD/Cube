using UnityEngine;

namespace Cube.Replication {
    [RequireComponent(typeof(ReplicaRigidbody))]
    public class ReplicaRigidbodyVisualiser : MonoBehaviour {
        ReplicaRigidbody _rb;

        void Start() {
            _rb = GetComponent<ReplicaRigidbody>();
        }

        void Update() {
            _rb.history.Sample(Time.time, out Vector3 dp, out Quaternion dr);
            for (var f = 0f; f < 0.025f; f += 0.001f) {
                _rb.history.Sample(Time.time - f, out Vector3 p, out Quaternion r);
                Debug.DrawLine(dp, p, Color.cyan);
                dp = p;
                dr = r;
            }
        }
    }
}