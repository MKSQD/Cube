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
        public Transform model;

        Rigidbody _rigidbody;
        Vector3 modelLastPos;
        Quaternion modelLastRot;

        const float maxVelocity = 16;
        const float maxAngularVelocity = 16;

        void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
        }

        void Update() {
            if (model == null || !isClient)
                return;

            var dist = (modelLastPos - transform.position).sqrMagnitude;
            if (dist < 10) {
                model.position = Vector3.Lerp(modelLastPos, transform.position, Time.deltaTime * 14);
                model.rotation = Quaternion.Lerp(modelLastRot, transform.rotation, Time.deltaTime * 14);
            }
            else {
                model.position = transform.position;
                model.rotation = transform.rotation;

            }
            modelLastPos = model.position;
            modelLastRot = model.rotation;
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

            var sleeping = bs.ReadBool();
            if (!sleeping) {
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
            else {
                _rigidbody.Sleep();
            }
        }
    }
}
