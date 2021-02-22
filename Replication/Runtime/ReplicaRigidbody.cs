using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <summary>
    /// Synced Rigidbody based on state synchronization.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaRigidbody")]
    [RequireComponent(typeof(Rigidbody))]
    public class ReplicaRigidbody : ReplicaBehaviour {
        [Tooltip("Visual representation of the object to be interpolated (seperate from the physical representation)")]
        public Transform model;

        new Rigidbody rigidbody;
        bool clientSleeping = false;

        const float maxVelocity = 16;
        const float maxAngularVelocity = 16;

        double m_InterpolationBackTime = 0.1;
        double m_ExtrapolationLimit = 0.5;

        // We store twenty states with "playback" information
        State[] m_BufferedState = new State[20];
        // Keep track of what slots are used
        int m_TimestampCount;

        internal struct State {
            internal double timestamp;
            internal Vector3 pos;
            internal Vector3 velocity;
            internal Quaternion rot;
            internal Vector3 angularVelocity;
        }

        void Awake() {
            rigidbody = GetComponent<Rigidbody>();
        }

        void Start() {
            m_InterpolationBackTime = Replica.settings.DesiredUpdateRate * 2.5f;
        }

        void Update() {
            if (model == null || !isClient)
                return;

#if UNITY_EDITOR
            if (Replica.isSceneReplica)
                return;
#endif

            UpdateTransform();
        }

        void UpdateTransform() {
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
                        //    Debug.Log(t);
                        // if t=0 => lhs is used directly
                        transform.localPosition = Vector3.Lerp(lhs.pos, rhs.pos, t);
                        transform.localRotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);
                        return;
                    }
                }
            }
            // Use extrapolation
            else {
                State latest = m_BufferedState[0];

                float extrapolationLength = (float)(interpolationTime - latest.timestamp);
                // Don't extrapolation for more than 500 ms, you would need to do that carefully
                if (extrapolationLength < m_ExtrapolationLimit) {
                    float axisLength = extrapolationLength * latest.angularVelocity.magnitude * Mathf.Rad2Deg;
                    Quaternion angularRotation = Quaternion.AngleAxis(axisLength, latest.angularVelocity);

                    transform.position = latest.pos + latest.velocity * extrapolationLength;
                    transform.rotation = angularRotation * latest.rot;
                    rigidbody.velocity = latest.velocity;
                    rigidbody.angularVelocity = latest.angularVelocity;
                }
            }
        }

        void FixedUpdate() {
            if (isClient) {
                if (clientSleeping) {
                    rigidbody.velocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }
            }
        }

        public override void Serialize(BitStream bs, SerializeContext ctx) {
            bs.Write(transform.position);

            var euler = transform.rotation.eulerAngles;
            if (euler.x < 0) euler.x += 360;
            if (euler.y < 0) euler.y += 360;
            if (euler.z < 0) euler.z += 360;
            bs.WriteLossyFloat(euler.x, 0, 360);
            bs.WriteLossyFloat(euler.y, 0, 360);
            bs.WriteLossyFloat(euler.z, 0, 360);

            var sleeping = rigidbody.IsSleeping();
            bs.Write(sleeping);
            if (sleeping)
                return;

            var velocity = rigidbody.velocity;
            bs.WriteLossyFloat(velocity.x, -maxVelocity, maxVelocity);
            bs.WriteLossyFloat(velocity.y, -maxVelocity, maxVelocity);
            bs.WriteLossyFloat(velocity.z, -maxVelocity, maxVelocity);

            var angularVelocity = rigidbody.angularVelocity;
            bs.WriteLossyFloat(angularVelocity.x, -maxAngularVelocity, maxAngularVelocity);
            bs.WriteLossyFloat(angularVelocity.y, -maxAngularVelocity, maxAngularVelocity);
            bs.WriteLossyFloat(angularVelocity.z, -maxAngularVelocity, maxAngularVelocity);
        }

        public override void Deserialize(BitStream bs) {
            var position = transform.position = bs.ReadVector3();

            var euler = new Vector3 {
                x = bs.ReadLossyFloat(0, 360),
                y = bs.ReadLossyFloat(0, 360),
                z = bs.ReadLossyFloat(0, 360)
            };
            var rotation = transform.rotation = Quaternion.Euler(euler);

            var velocity = Vector3.zero;
            var angularVelocity = Vector3.zero;
            clientSleeping = bs.ReadBool();
            if (!clientSleeping) {
                velocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    y = bs.ReadLossyFloat(-maxVelocity, maxVelocity),
                    z = bs.ReadLossyFloat(-maxVelocity, maxVelocity)
                };
                angularVelocity = new Vector3 {
                    x = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    y = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity),
                    z = bs.ReadLossyFloat(-maxAngularVelocity, maxAngularVelocity)
                };
            }

            // Shift the buffer sideways, deleting state 20
            for (int i = m_BufferedState.Length - 1; i >= 1; i--) {
                m_BufferedState[i] = m_BufferedState[i - 1];
            }

            // Record current state in slot 0
            State state;
            state.timestamp = Time.timeAsDouble;
            state.pos = position;
            state.velocity = velocity;
            state.rot = rotation;
            state.angularVelocity = angularVelocity;
            m_BufferedState[0] = state;

            m_TimestampCount = Mathf.Min(m_TimestampCount + 1, m_BufferedState.Length);
        }
    }
}
