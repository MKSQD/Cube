using UnityEngine;
using BitStream = Cube.Networking.Transport.BitStream;

namespace Cube.Networking.Replicas {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube.Networking.Replicas/ReplicaRigidbody")]
    [RequireComponent(typeof(Rigidbody))]
    public class ReplicaRigidbody : ReplicaBehaviour {
        public GameObject model;

        Rigidbody _rigidbody;
        Vector3 _modelOffset;
        Quaternion _modelRotationOffset;
        Vector3 _lastPos;
        Quaternion _lastRot;

        void Awake() {
            _rigidbody = GetComponent<Rigidbody>();

            if (model != null) {
                _lastPos = transform.position;
                _lastRot = transform.rotation;
                _modelOffset = model.transform.localPosition;
                _modelRotationOffset = model.transform.localRotation;
            }
        }

#if CLIENT
        void Update() {
            if (model == null)
                return;

            var newPos = transform.position + _modelOffset;
            var newRot = transform.rotation * _modelRotationOffset;

            if ((_lastPos - newPos).sqrMagnitude < 0.5f) {
                model.transform.position = Vector3.Lerp(_lastPos, newPos, Time.deltaTime * 8);
                model.transform.rotation = Quaternion.Slerp(_lastRot, newRot, Time.deltaTime * 12);
            }
            else {
                model.transform.position = newPos;
                model.transform.rotation = newRot;
            }
            
            _lastPos = model.transform.position;
            _lastRot = model.transform.rotation;
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
            if (sleeping) {
                _rigidbody.Sleep();


                return;
            }

            _rigidbody.velocity = bs.ReadVector3();
            _rigidbody.angularVelocity = bs.ReadVector3();

        }
#endif
    }
}
