using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <summary>
    /// Synchronize position, rotation and transform.parent.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaTransform")]
    public class ReplicaTransform : ReplicaBehaviour {
        public enum Interpolation {
            Raw,
            Interpolate
        }

#if CLIENT || UNITY_EDITOR
        public Interpolation interpolation = Interpolation.Interpolate;
#endif

        [Range(0, 800)]
        public int interpolateDelayMs;

        TransformHistory _history;
        protected TransformHistory history {
            get {
                if (_history == null) {
                    _history = new TransformHistory(interpolateDelayMs * 0.001f * 2);
                }
                return _history;
            }
        }

#if CLIENT
        Vector3 _lastPos;
        Vector3 _velocity;
        void Update() {
            if (isClient && interpolation != Interpolation.Raw) {
                var position = transform.position;
                var rotation = transform.rotation;

                _velocity = transform.position - _lastPos;
                _lastPos = transform.position;

                var velocity = Vector3.zero;
                history.Read(Time.time, ref position, ref velocity, ref rotation);

                transform.position = position;
                transform.rotation = rotation;
            }
        }
#endif

#if SERVER
        public override void Serialize(BitStream bs, ReplicaSerializationMode mode, ReplicaView view) {
            bs.Write(transform.position);
            bs.Write(transform.rotation);
        }
#endif

#if CLIENT
        public override void Deserialize(BitStream bs, ReplicaSerializationMode mode) {
            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            if (interpolation == Interpolation.Raw) {
                transform.position = position;
                transform.rotation = rotation;
            }
            else if (interpolation == Interpolation.Interpolate) {
                history.Write(Time.time + interpolateDelayMs * 0.001f, position, _velocity, rotation);
            }
        }
#endif
    }
}
