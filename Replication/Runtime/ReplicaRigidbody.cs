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
        void Update() {
            if (model == null || !isClient)
                return;
            
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
            bs.Write(transform.rotation);

            var sleeping = _rigidbody.IsSleeping();
            bs.Write(sleeping);
            if (sleeping)
                return;

            bs.Write(_rigidbody.velocity);
            bs.Write(_rigidbody.angularVelocity);
        }
#endif

#if CLIENT
        public override void Deserialize(BitStream bs, ReplicaSerializationMode mode) {
            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();
            
            transform.position = position;
            transform.rotation = rotation;

            var sleeping = bs.ReadBool();
            if (!sleeping) {
                _rigidbody.velocity = bs.ReadVector3();
                _rigidbody.angularVelocity = bs.ReadVector3();
            }
            else {
                _rigidbody.Sleep();
            }
            
            _history.Write(Time.time + interpolateDelayMs * 0.001f, position, _rigidbody.velocity, rotation);
        }
#endif
    }
}
