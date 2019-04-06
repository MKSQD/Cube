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
                    _history = new TransformHistory(Constants.replicaUpdateRateMS * 0.001f, 1f);
                }
                return _history;
            }
        }
        
        public bool sendFullUpdateOnly;

#if CLIENT
#if UNITY_EDITOR
        Vector3 _lastPos;
#endif
        void Update() {
            if (isClient && interpolation != Interpolation.Raw) {
#if UNITY_EDITOR
                Debug.DrawLine(_lastPos, transform.position, Color.red, 0.8f);
                _lastPos = transform.position;
#endif

                var position = transform.localPosition;
                var rotation = transform.localRotation;

                var velocity = Vector3.zero;
                history.Read(Time.time, ref position, ref velocity, ref rotation);

                transform.localPosition = position;
                transform.localRotation = rotation;
            }
        }
#endif

#if SERVER
        public override void Serialize(BitStream bs, ReplicaSerializationMode mode, ReplicaView view) {
            if (sendFullUpdateOnly && mode != ReplicaSerializationMode.Full)
                return;

            bs.Write(transform.position);
            bs.Write(transform.rotation);
        }
#endif

#if CLIENT
        public override void Deserialize(BitStream bs, ReplicaSerializationMode mode) {
            if (sendFullUpdateOnly && mode != ReplicaSerializationMode.Full)
                return;

            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            if (interpolation == Interpolation.Raw) {
                transform.position = position;
                transform.rotation = rotation;
            }
            else if (interpolation == Interpolation.Interpolate) {
                var velocity = Vector3.zero;
                history.Write(Time.time + interpolateDelayMs * 0.001f, position, velocity, rotation);
            }
        }
#endif
    }
}
