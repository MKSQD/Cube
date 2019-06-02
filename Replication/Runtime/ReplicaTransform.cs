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
        
#if UNITY_EDITOR
        public Interpolation interpolation = Interpolation.Interpolate;


        [Range(0, 800)]
        public int interpolateDelayMs;
#endif

        TransformHistory _history;

        void Awake() {
            _history = new TransformHistory(interpolateDelayMs * 0.001f * 2);
        }

        void Update() {
            if (isClient && interpolation != Interpolation.Raw) {
                var position = transform.position;
                var rotation = transform.rotation;

                _history.Read(Time.time, ref position, ref rotation);

                transform.position = position;
                transform.rotation = rotation;
            }
        }

        public override void Serialize(BitStream bs, ReplicaView view) {
            bs.Write(transform.position);
            bs.Write(transform.rotation);
        }

        public override void Deserialize(BitStream bs) {
            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            if (interpolation == Interpolation.Raw) {
                transform.position = position;
                transform.rotation = rotation;
            }
            else if (interpolation == Interpolation.Interpolate) {
                _history.Write(Time.time + interpolateDelayMs * 0.001f, position, Vector3.zero, rotation);
            }
        }
    }
}
