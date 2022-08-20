using System;
using Cube.Transport;
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
    public sealed class ReplicaRigidbody : ReplicaBehaviour {
        [Tooltip("Visual representation of the object to be interpolated (seperate from the physical representation)")]
        [FormerlySerializedAs("model")]
        public Transform Model;

        public static ReplicaRigidbodySettings DefaultSettings;
        public ReplicaRigidbodySettings Settings;
        public ReplicaRigidbodySettings SettingsOrDefault => Settings ?? DefaultSettings;

        Rigidbody _rigidbody;

        Vector3 _positionError;
        Quaternion _rotationError;

        void Awake() {
            _rigidbody = GetComponent<Rigidbody>();

            if (Settings == null) {
                if (DefaultSettings == null) {
                    DefaultSettings = ScriptableObject.CreateInstance<ReplicaRigidbodySettings>();
                }
                Settings = DefaultSettings;
            }
        }

        void Update() {
            if (isClient) {
                var smoothing = 0.95f;

                if (_positionError.sqrMagnitude > 0.000001f) {
                    _positionError *= smoothing;
                } else {
                    _positionError = Vector3.zero;
                }

                if (Math.Abs(_rotationError.x) > 0.000001f ||
                            Math.Abs(_rotationError.y) > 0.000001f ||
                            Math.Abs(_rotationError.y) > 0.000001f ||
                            Math.Abs(1.0f - _rotationError.w) > 0.000001f) {
                    _rotationError = Quaternion.Slerp(_rotationError, Quaternion.identity, 1 - smoothing);
                } else {
                    _rotationError = Quaternion.identity;
                }

                Model.position = transform.position + _positionError;
                Model.rotation = transform.rotation * _rotationError;
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


                bs.WriteLossyFloat(velocity.x, -maxVelocity, maxVelocity, Settings.VelocityPrecision);
                bs.WriteLossyFloat(velocity.y, -maxVelocity, maxVelocity, Settings.VelocityPrecision);
                bs.WriteLossyFloat(velocity.z, -maxVelocity, maxVelocity, Settings.VelocityPrecision);

                var maxAngularVelocity = Settings.MaxAngularVelocity;
                var angularVelocity = _rigidbody.angularVelocity;
                bs.WriteLossyFloat(angularVelocity.x, -maxAngularVelocity, maxAngularVelocity, Settings.AngularVelocityPrecision);
                bs.WriteLossyFloat(angularVelocity.y, -maxAngularVelocity, maxAngularVelocity, Settings.AngularVelocityPrecision);
                bs.WriteLossyFloat(angularVelocity.z, -maxAngularVelocity, maxAngularVelocity, Settings.AngularVelocityPrecision);
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

            _positionError = (transform.position + _positionError) - position;
            _rotationError = Quaternion.Inverse(rotation) * (transform.rotation * _rotationError);

            if (_positionError.sqrMagnitude >= Settings.TeleportDistanceSqr) {
                _positionError = Vector3.zero;
                _rotationError = Quaternion.identity;
            }

            transform.position = position;
            transform.rotation = rotation;

            var sleeping = bs.ReadBool();
            if (!sleeping) {
                var maxVelocity = Settings.MaxVelocity;
                var velocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxVelocity, maxVelocity, Settings.VelocityPrecision),
                    y = bs.ReadLossyFloat(-maxVelocity, maxVelocity, Settings.VelocityPrecision),
                    z = bs.ReadLossyFloat(-maxVelocity, maxVelocity, Settings.VelocityPrecision)
                };

                var maxAngularVelocity = Settings.MaxAngularVelocity;
                var angularVelocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity, Settings.AngularVelocityPrecision),
                    y = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity, Settings.AngularVelocityPrecision),
                    z = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity, Settings.AngularVelocityPrecision)
                };

                _rigidbody.velocity = velocity;
                _rigidbody.angularVelocity = angularVelocity;
            } else if (!_rigidbody.IsSleeping()) {
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.Sleep();
            }
        }
    }
}
