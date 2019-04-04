using UnityEngine;
using BitStream = Cube.Networking.Transport.BitStream;

namespace Cube.Networking.Replicas {
    /// <summary>
    /// Synchronize position, rotation and transform.parent.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube.Networking.Replicas/ReplicaTransform")]
    public class ReplicaTransform : ReplicaBehaviour {
        public enum Mode {
            Raw,
            Interpolate
        }

#if CLIENT
        [SerializeField]
        Mode _mode = Mode.Raw;
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

        [SerializeField]
        bool _sendFullUpdateOnly;

#if CLIENT
#if UNITY_EDITOR
        Vector3 _lastPos;
#endif
        void Update() {
            if (isClient && _mode != Mode.Raw) {
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
            if (_sendFullUpdateOnly && mode != ReplicaSerializationMode.Full)
                return;

            bs.Write(transform.position);
            bs.Write(transform.rotation);
        }
#endif

#if CLIENT
        public override void Deserialize(BitStream bs, ReplicaSerializationMode mode) {
            if (_sendFullUpdateOnly && mode != ReplicaSerializationMode.Full)
                return;

            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            if (_mode == Mode.Raw) {
                transform.position = position;
                transform.rotation = rotation;
            }
            else if (_mode == Mode.Interpolate) {
                var velocity = Vector3.zero;
                history.Write(Time.time + interpolateDelayMs * 0.001f, position, velocity, rotation);
            }
        }
#endif
    }
}
