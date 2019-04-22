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

        [Range(0, 800)]
        public int interpolateDelayMs;

        TransformHistory _history;

        Rigidbody _rigidbody;
        Vector3 _modelOffset;
        Quaternion _modelRotationOffset;

        void Awake() {
            _history = new TransformHistory(interpolateDelayMs * 0.001f * 2);

            if (model != null) {
                _modelOffset = model.localPosition;
                _modelRotationOffset = model.localRotation;
            }

            _rigidbody = GetComponent<Rigidbody>();
        }

#if CLIENT
        float _lastTime;
        void Update() {
            if (model == null || !isClient)
                return;

            if (Time.time - _lastTime > 0.1f) {
                _lastTime = Time.time;
                _history.Write(Time.time + interpolateDelayMs * 0.001f, transform.position, _rigidbody.velocity, transform.rotation);
            }

            var position = transform.position;
            var rotation = transform.rotation;

            _history.Read(Time.time, ref position, ref rotation);

            model.position = position + _modelOffset;
            model.rotation = rotation * _modelRotationOffset;
        }
#endif

#if SERVER
        public override void Serialize(BitStream bs, ReplicaSerializationMode mode, ReplicaView view) {
            bs.Write(transform.position);

            var euler = transform.rotation.eulerAngles;
            if (euler.x < 0) euler.x += 360;
            if (euler.y < 0) euler.y += 360;
            if (euler.z < 0) euler.z += 360;
            bs.CompressFloat(euler.x, 0, 360, 0.1f);
            bs.CompressFloat(euler.y, 0, 360, 0.1f);
            bs.CompressFloat(euler.z, 0, 360, 0.1f);

            var sleeping = _rigidbody.IsSleeping();
            bs.Write(sleeping);
            if (!sleeping) {
                var velocity = _rigidbody.velocity;
                bs.CompressFloat(velocity.x, -10, 10);
                bs.CompressFloat(velocity.y, -10, 10);
                bs.CompressFloat(velocity.z, -10, 10);

                bs.Write(_rigidbody.angularVelocity);
            }
        }
#endif

#if CLIENT
        public override void Deserialize(BitStream bs, ReplicaSerializationMode mode) {
            transform.position = bs.ReadVector3();

            var euler = new Vector3 {
                x = bs.DecompressFloat(0, 360, 0.1f),
                y = bs.DecompressFloat(0, 360, 0.1f),
                z = bs.DecompressFloat(0, 360, 0.1f)
            };
            transform.rotation = Quaternion.Euler(euler);

            var sleeping = bs.ReadBool();
            if (!sleeping) {
                var velocity = new Vector3();
                velocity.x = bs.DecompressFloat(-10, 10);
                velocity.y = bs.DecompressFloat(-10, 10);
                velocity.z = bs.DecompressFloat(-10, 10);
                _rigidbody.velocity = velocity;

                _rigidbody.angularVelocity = bs.ReadVector3();
            }
            else {
                _rigidbody.Sleep();
            }
        }
#endif
    }
}
