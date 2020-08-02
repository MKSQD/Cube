using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <summary>
    /// Synced Rigidbody based on state synchronization.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaRigidbody")]
    [RequireComponent(typeof(Rigidbody))]
    public class ReplicaRigidbody : ReplicaBehaviour {
        [Tooltip("Visual representation of the object to be interpolated (seperate from the physical representation)")]
        public Transform model;

        public TransformHistory history;

        Rigidbody _rigidbody;
        bool _clientSleeping = false;

        const float maxVelocity = 16;
        const float maxAngularVelocity = 16;

        void Awake() {
            _rigidbody = GetComponent<Rigidbody>();

            history = new TransformHistory(25, 500);
        }

        void Update() {
            if (model == null || !isClient)
                return;

            history.Add(Time.time + 0.025f, new Pose(transform.position, transform.rotation));

            history.Sample(Time.time, out Vector3 pos, out Quaternion rot);
            model.position = pos;
            model.rotation = rot;
        }

        void FixedUpdate() {
            if (isClient && _clientSleeping) {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
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
                bs.WriteLossyFloat(velocity.x, -maxVelocity, maxVelocity);
                bs.WriteLossyFloat(velocity.y, -maxVelocity, maxVelocity);
                bs.WriteLossyFloat(velocity.z, -maxVelocity, maxVelocity);

                var angularVelocity = _rigidbody.angularVelocity;
                bs.WriteLossyFloat(angularVelocity.x, -maxAngularVelocity, maxAngularVelocity);
                bs.WriteLossyFloat(angularVelocity.y, -maxAngularVelocity, maxAngularVelocity);
                bs.WriteLossyFloat(angularVelocity.z, -maxAngularVelocity, maxAngularVelocity);
            }
        }

        public override void Deserialize(BitStream bs) {
            transform.position = bs.ReadVector3();

            var euler = new Vector3 {
                x = bs.ReadLossyFloat(0, 360),
                y = bs.ReadLossyFloat(0, 360),
                z = bs.ReadLossyFloat(0, 360)
            };
            transform.rotation = Quaternion.Euler(euler);

            _clientSleeping = bs.ReadBool();
            if (!_clientSleeping) {
                var velocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    y = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    z = bs.ReadLossyFloat(-maxVelocity, maxVelocity)
                };
                _rigidbody.velocity = velocity;

                var angularVelocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    y = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    z = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity)
                };
                _rigidbody.angularVelocity = angularVelocity;
            }
        }
    }
}
