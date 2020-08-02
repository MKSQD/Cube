﻿using UnityEngine;
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
        
        public Interpolation interpolation = Interpolation.Interpolate;
        
        [Range(0, 500)]
        public int interpolationDelayMs;

        TransformHistory _history;

        void Awake() {
            _history = new TransformHistory(replica.settingsOrDefault.desiredUpdateRateMs, interpolationDelayMs);
        }

        void Update() {
            if (isClient && interpolation != Interpolation.Raw) {
                _history.Sample(Time.time, out Vector3 position, out Quaternion rotation);
                transform.position = position;
                transform.rotation = rotation;
            }
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
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
                var t = Time.time + interpolationDelayMs * 0.001f;
                _history.Add(t, new Pose(position, rotation));
            }
        }
    }
}
