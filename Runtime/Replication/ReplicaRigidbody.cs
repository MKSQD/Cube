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
        public ReplicaRigidbodySettings SettingsOrDefault => Settings != null ? Settings : DefaultSettings;

        Rigidbody _rigidbody;

        Vector3 _positionError;
        Quaternion _rotationError;

        void Awake() {
            if (Settings == null) {
                if (DefaultSettings == null) {
                    DefaultSettings = ScriptableObject.CreateInstance<ReplicaRigidbodySettings>();
                }
                Settings = DefaultSettings;
            }

            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.maxLinearVelocity = Settings.MaxVelocity;
            _rigidbody.maxAngularVelocity = Settings.MaxAngularVelocity;
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

                Model.position = _rigidbody.position + _positionError;
                Model.rotation = _rigidbody.rotation * _rotationError;
            }
        }

        public override void Serialize(IBitWriter bs, SerializeContext ctx) {
            bs.WriteLossyFloat(_rigidbody.position.x, Settings.WorldBounds.MinWorldX, Settings.WorldBounds.MaxWorldX, 0.01f);
            bs.WriteLossyFloat(_rigidbody.position.y, Settings.WorldBounds.MinWorldY, Settings.WorldBounds.MaxWorldY, 0.01f);
            bs.WriteLossyFloat(_rigidbody.position.z, Settings.WorldBounds.MinWorldZ, Settings.WorldBounds.MaxWorldZ, 0.01f);

            var rot = BitWriter.QuantizeQuaternion(_rigidbody.rotation);
            bs.WriteQuaternion(rot);
            _rigidbody.rotation = rot;

            var sleeping = _rigidbody.IsSleeping();
            bs.WriteBool(sleeping);
            if (!sleeping) {
                var maxVelocity = Settings.MaxVelocity;
                var velocity = _rigidbody.velocity;

#if UNITY_EDITOR
                if (velocity.x > maxVelocity || velocity.x < -maxVelocity
                    || velocity.y > maxVelocity || velocity.y < -maxVelocity
                    || velocity.z > maxVelocity || velocity.z < -maxVelocity) {
                    Debug.LogWarning("Velocity out of range. To change the rigidbody's max velocity, change ReplicaRigidbody settings.", gameObject);
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
            Vector3 pos;
            pos.x = bs.ReadLossyFloat(Settings.WorldBounds.MinWorldX, Settings.WorldBounds.MaxWorldX, 0.01f);
            pos.y = bs.ReadLossyFloat(Settings.WorldBounds.MinWorldY, Settings.WorldBounds.MaxWorldY, 0.01f);
            pos.z = bs.ReadLossyFloat(Settings.WorldBounds.MinWorldZ, Settings.WorldBounds.MaxWorldZ, 0.01f);

            var rotation = bs.ReadQuaternion();

            _positionError = (_rigidbody.position + _positionError) - pos;
            _rotationError = Quaternion.Inverse(rotation) * (_rigidbody.rotation * _rotationError);

            if (Replica.lastUpdateTime < 0.001f || _positionError.sqrMagnitude >= Settings.TeleportDistanceSqr) {
                _positionError = Vector3.zero;
                _rotationError = Quaternion.identity;
            }

            // Instantly hard snap the state to the received state
            // The Model Transform is used to smooth the resulting error
            _rigidbody.position = pos;
            _rigidbody.rotation = rotation;

            Model.position = pos + _positionError;
            Model.rotation = rotation * _rotationError;

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
