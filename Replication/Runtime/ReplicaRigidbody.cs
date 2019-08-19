using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaRigidbody")]
    [RequireComponent(typeof(Rigidbody))]
    public class ReplicaRigidbody : ReplicaBehaviour {
        public Transform model;

        [Range(0, 500)]
        public int interpolationDelayMs;

        TransformHistory _history;

        Rigidbody _rigidbody;

        void Awake() {
            _history = new TransformHistory();

            _rigidbody = GetComponent<Rigidbody>();
        }

        void Update() {
            if (model == null || !isClient)
                return;
            
            _history.Sample(Time.time, out Vector3 position, out Quaternion rotation);
            model.position = position;
            model.rotation = rotation;
        }

        void FixedUpdate() {
            if (model == null || !isClient)
                return;

            _history.Add(new Pose(transform.position, transform.rotation), Time.time + interpolationDelayMs * 0.001f);
        }

        public override void Serialize(BitStream bs, ReplicaView view) {
            bs.Write(enabled);
            if (!enabled)
                return;
            
            bs.Write(transform.position);

            var euler = transform.rotation.eulerAngles;
            if (euler.x < 0) euler.x += 360;
            if (euler.y < 0) euler.y += 360;
            if (euler.z < 0) euler.z += 360;
            bs.WriteLossyFloat(euler.x, 0, 360);
            bs.WriteLossyFloat(euler.y, 0, 360);
            bs.WriteLossyFloat(euler.z, 0, 360);

            var sleeping = _rigidbody.IsSleeping();
            bs.Write(sleeping);
            if (!sleeping) {
                var velocity = _rigidbody.velocity;
                velocity.x = Mathf.Clamp(velocity.x, -20, 20);
                velocity.y = Mathf.Clamp(velocity.y, -20, 20);
                velocity.z = Mathf.Clamp(velocity.z, -20, 20);
                bs.WriteLossyFloat(velocity.x, -20, 20);
                bs.WriteLossyFloat(velocity.y, -20, 20);
                bs.WriteLossyFloat(velocity.z, -20, 20);

                var angularVelocity = _rigidbody.angularVelocity;
                angularVelocity.x = Mathf.Clamp(angularVelocity.x, -20, 20);
                angularVelocity.y = Mathf.Clamp(angularVelocity.y, -20, 20);
                angularVelocity.z = Mathf.Clamp(angularVelocity.z, -20, 20);
                bs.WriteLossyFloat(angularVelocity.x, -10, 10);
                bs.WriteLossyFloat(angularVelocity.y, -10, 10);
                bs.WriteLossyFloat(angularVelocity.z, -10, 10);
            }
        }

        public override void Deserialize(BitStream bs) {
            var isEnabled = bs.ReadBool();
            enabled = isEnabled;
            if (!isEnabled)
                return;

            transform.position = bs.ReadVector3();

            var euler = new Vector3 {
                x = bs.ReadLossyFloat(0, 360),
                y = bs.ReadLossyFloat(0, 360),
                z = bs.ReadLossyFloat(0, 360)
            };
            transform.rotation = Quaternion.Euler(euler);

            var sleeping = bs.ReadBool();
            if (!sleeping) {
                var velocity = new Vector3 {
                    x = bs.ReadLossyFloat(-20, 20),
                    y = bs.ReadLossyFloat(-20, 20),
                    z = bs.ReadLossyFloat(-20, 20)
                };
                _rigidbody.velocity = velocity;

                var angularVelocity = new Vector3 {
                    x = bs.ReadLossyFloat(-10, 10),
                    y = bs.ReadLossyFloat(-10, 10),
                    z = bs.ReadLossyFloat(-10, 10)
                };
                _rigidbody.angularVelocity = angularVelocity;
            }
            else {
                _rigidbody.Sleep();
            }
        }
    }
}
