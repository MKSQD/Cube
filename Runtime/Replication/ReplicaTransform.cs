using Cube.Transport;
using UnityEngine;


namespace Cube.Replication {
    /// <summary>
    /// Synchronize position, rotation and transform.parent.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaTransform")]
    public class ReplicaTransform : ReplicaBehaviour {
        // Interpolation
        double m_InterpolationBackTime = 0.1;

        struct State {
            internal double timestamp;
            internal Vector3 pos;
            internal Quaternion rot;
        }

        State[] m_BufferedState = new State[20];
        int m_TimestampCount;

        public override void Serialize(BitWriter bs, SerializeContext ctx) {
            bs.WriteVector3(transform.position);
            bs.WriteQuaternion(transform.rotation);
        }

        public override void Deserialize(BitReader bs) {
            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            // Shift the buffer sideways, deleting state 20
            for (int i = m_BufferedState.Length - 1; i >= 1; i--) {
                m_BufferedState[i] = m_BufferedState[i - 1];
            }

            // Record current state in slot 0
            State state;
            state.timestamp = Time.timeAsDouble;
            state.pos = position;
            state.rot = rotation;
            m_BufferedState[0] = state;

            m_TimestampCount = Mathf.Min(m_TimestampCount + 1, m_BufferedState.Length);
        }

        void Update() {
            // This is the target playback time of the rigid body
            double interpolationTime = Time.timeAsDouble - m_InterpolationBackTime;

            // Use interpolation if the target playback time is present in the buffer
            if (m_BufferedState[0].timestamp > interpolationTime) {
                // Go through buffer and find correct state to play back
                for (int i = 0; i < m_TimestampCount; i++) {
                    if (m_BufferedState[i].timestamp <= interpolationTime || i == m_TimestampCount - 1) {
                        // The state one slot newer (<100ms) than the best playback state
                        State rhs = m_BufferedState[Mathf.Max(i - 1, 0)];
                        // The best playback state (closest to 100 ms old (default time))
                        State lhs = m_BufferedState[i];

                        // Use the time between the two slots to determine if interpolation is necessary
                        double length = rhs.timestamp - lhs.timestamp;
                        float t = 0.0F;
                        // As the time difference gets closer to 100 ms t gets closer to 1 in 
                        // which case rhs is only used
                        // Example:
                        // Time is 10.000, so sampleTime is 9.900 
                        // lhs.time is 9.910 rhs.time is 9.980 length is 0.070
                        // t is 9.900 - 9.910 / 0.070 = 0.14. So it uses 14% of rhs, 86% of lhs
                        if (length > 0.0001) {
                            t = (float)((interpolationTime - lhs.timestamp) / length);
                        }
                        //	Debug.Log(t);
                        // if t=0 => lhs is used directly
                        transform.localPosition = Vector3.Lerp(lhs.pos, rhs.pos, t);
                        transform.localRotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);
                        return;
                    }
                }
            }
        }
    }
}
