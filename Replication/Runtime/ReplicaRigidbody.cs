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

        new Rigidbody rigidbody;

        const float maxVelocity = 16;
        const float maxAngularVelocity = 16;

        Vector3 currentModelPosition;
        Quaternion currentModelRotation;
        float blend;

        void Awake() {
            rigidbody = GetComponent<Rigidbody>();
        }

        void Update() {
            if (model != null) {
                if (blend < 1) {
                    blend = Mathf.Min(blend + Time.deltaTime * 3, 1);

                    currentModelPosition = Vector3.Lerp(currentModelPosition, transform.position, blend);
                    currentModelRotation = Quaternion.Lerp(currentModelRotation, transform.rotation, blend);
                    model.position = currentModelPosition;
                    model.rotation = currentModelRotation;
                } else {
                    currentModelPosition = transform.position;
                    currentModelRotation = transform.rotation;
                }
            }
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            bs.Write(transform.position);

            var euler = transform.rotation.eulerAngles;
            if (euler.x < 0)
                euler.x += 360;
            if (euler.y < 0)
                euler.y += 360;
            if (euler.z < 0)
                euler.z += 360;
            bs.WriteLossyFloat(euler.x, 0, 360);
            bs.WriteLossyFloat(euler.y, 0, 360);
            bs.WriteLossyFloat(euler.z, 0, 360);

            var sleeping = rigidbody.IsSleeping();
            bs.Write(sleeping);
            if (sleeping)
                return;

            var velocity = rigidbody.velocity;
            bs.WriteLossyFloat(velocity.x, -maxVelocity, maxVelocity);
            bs.WriteLossyFloat(velocity.y, -maxVelocity, maxVelocity);
            bs.WriteLossyFloat(velocity.z, -maxVelocity, maxVelocity);

            var angularVelocity = rigidbody.angularVelocity;
            bs.WriteLossyFloat(angularVelocity.x, -maxAngularVelocity, maxAngularVelocity);
            bs.WriteLossyFloat(angularVelocity.y, -maxAngularVelocity, maxAngularVelocity);
            bs.WriteLossyFloat(angularVelocity.z, -maxAngularVelocity, maxAngularVelocity);
        }

        public override void Deserialize(BitStream bs) {
            var position = bs.ReadVector3();

            var euler = new Vector3 {
                x = bs.ReadLossyFloat(0, 360),
                y = bs.ReadLossyFloat(0, 360),
                z = bs.ReadLossyFloat(0, 360)
            };
            var rotation = transform.rotation = Quaternion.Euler(euler);

            var velocity = Vector3.zero;
            var angularVelocity = Vector3.zero;
            var sleeping = bs.ReadBool();
            if (!sleeping) {
                velocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    y = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    z = bs.ReadLossyFloat(-maxVelocity, maxVelocity)
                };
                angularVelocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    y = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    z = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity)
                };
            }

            transform.position = position;
            transform.rotation = rotation;
            rigidbody.velocity = velocity;
            rigidbody.angularVelocity = angularVelocity;
            if (sleeping) {
                rigidbody.Sleep();
            }

            blend = 0;
        }
    }
}
