using Cube.Transport;
using UnityEngine;


namespace Cube.Replication {
    /// <summary>
    /// Synchronize position, rotation and transform.parent.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaTransform")]
    public class ReplicaTransform : ReplicaBehaviour {
        struct State {
            public float Timestamp;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        State[] _bufferedState = new State[20];
        int _numTimestamps;

        public override void Serialize(IBitWriter bs, SerializeContext ctx) {
            bs.WriteVector3(transform.position);
            bs.WriteQuaternion(transform.rotation);
        }

        public override void Deserialize(BitReader bs) {
            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            // Shift the buffer sideways, deleting state 20
            for (int i = _bufferedState.Length - 1; i >= 1; i--) {
                _bufferedState[i] = _bufferedState[i - 1];
            }

            // Record current state in slot 0
            State state;
            state.Timestamp = Time.time + Replica.SettingsOrDefault.DesiredUpdateRate * 2.5f;
            state.Position = position;
            state.Rotation = rotation;
            _bufferedState[0] = state;

            _numTimestamps = Mathf.Min(_numTimestamps + 1, _bufferedState.Length);
        }

        void Update() {
            var interpolationTime = Time.time;

            if (_bufferedState[0].Timestamp > interpolationTime) {
                for (int i = 0; i < _numTimestamps; i++) {
                    if (_bufferedState[i].Timestamp <= interpolationTime || i == _numTimestamps - 1) {
                        State rhs = _bufferedState[Mathf.Max(i - 1, 0)];
                        State lhs = _bufferedState[i];

                        double length = rhs.Timestamp - lhs.Timestamp;
                        float t = 0.0F;
                        if (length > 0.0001) {
                            t = (float)((interpolationTime - lhs.Timestamp) / length);
                        }
                        transform.localPosition = Vector3.Lerp(lhs.Position, rhs.Position, t);
                        transform.localRotation = Quaternion.Slerp(lhs.Rotation, rhs.Rotation, t);
                        return;
                    }
                }
            }
        }
    }
}
