﻿using Cube.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Cube.Replication {
    /// <summary>
    /// Synced Rigidbody based on state synchronization.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaRigidbody")]
    [RequireComponent(typeof(Rigidbody))]
    public class ReplicaRigidbody : ReplicaBehaviour {
        [Tooltip("Visual representation of the object to be interpolated (seperate from the physical representation)")]
        [FormerlySerializedAs("model")]
        public Transform Model;

        public ReplicaRigidbodySettings Settings;

        Rigidbody _rigidbody;

        Vector3 _positionError;
        Quaternion _rotationError;

        protected void Awake() {
            _rigidbody = GetComponent<Rigidbody>();
        }

        protected void Update() {
            if (isClient) {
                Model.position = transform.position + _positionError;

                // _positionError.magnitude // 0.25 -> 0
                // _positionError.magnitude // 1 -> 1
                _positionError *= Mathf.Lerp(0.95f, 0.85f, (_positionError.magnitude - 0.25f) / 0.75f);
            }
        }

        public override void Serialize(IBitWriter bs, SerializeContext ctx) {
            bs.WriteVector3(transform.position);

            var euler = transform.rotation.eulerAngles;
            euler.x = Mathf.Repeat(euler.x, 360);
            euler.y = Mathf.Repeat(euler.y, 360);
            euler.z = Mathf.Repeat(euler.z, 360);
            bs.WriteLossyFloat(euler.x, 0, 360);
            bs.WriteLossyFloat(euler.y, 0, 360);
            bs.WriteLossyFloat(euler.z, 0, 360);

            var sleeping = _rigidbody.IsSleeping();
            bs.WriteBool(sleeping);
            if (!sleeping) {
                var maxVelocity = Settings.MaxVelocity;
                var velocity = _rigidbody.velocity;

#if UNITY_EDITOR
                if (velocity.x > maxVelocity || velocity.x < -maxVelocity
                    || velocity.y > maxVelocity || velocity.y < -maxVelocity
                    || velocity.z > maxVelocity || velocity.z < -maxVelocity) {
                    Debug.LogWarning("Velocity exceeded MaxVelocity (change settings or limit Rigidbody)");
                }
#endif


                bs.WriteLossyFloat(velocity.x, -maxVelocity, maxVelocity);
                bs.WriteLossyFloat(velocity.y, -maxVelocity, maxVelocity);
                bs.WriteLossyFloat(velocity.z, -maxVelocity, maxVelocity);

                var maxAngularVelocity = Settings.MaxAngularVelocity;
                var angularVelocity = _rigidbody.angularVelocity;
                bs.WriteLossyFloat(angularVelocity.x, -maxAngularVelocity, maxAngularVelocity);
                bs.WriteLossyFloat(angularVelocity.y, -maxAngularVelocity, maxAngularVelocity);
                bs.WriteLossyFloat(angularVelocity.z, -maxAngularVelocity, maxAngularVelocity);
            }
        }

        public override void Deserialize(BitReader bs) {
            var position = bs.ReadVector3();

            var euler = new Vector3 {
                x = bs.ReadLossyFloat(0, 360),
                y = bs.ReadLossyFloat(0, 360),
                z = bs.ReadLossyFloat(0, 360)
            };
            var rotation = Quaternion.Euler(euler);

            _positionError = position - (transform.position + _positionError);
            if (_positionError.sqrMagnitude > 1) {
                _positionError = Vector3.zero;
            }

            transform.position = position;
            transform.rotation = rotation;

            var sleeping = bs.ReadBool();
            if (!sleeping) {
                var maxVelocity = Settings.MaxVelocity;
                var velocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    y = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    z = bs.ReadLossyFloat(-maxVelocity, maxVelocity)
                };

                var maxAngularVelocity = Settings.MaxAngularVelocity;
                var angularVelocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    y = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    z = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity)
                };

                _rigidbody.velocity = velocity;
                _rigidbody.angularVelocity = angularVelocity;
            } else if (!_rigidbody.IsSleeping()) {
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.Sleep();
            }

            DebugExt.DrawWireSphere(position, 0.05f, Color.red, 10);
        }
    }
}
