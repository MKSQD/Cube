using Cube.Transport;
using UnityEngine;


namespace Cube.Replication {
    /// <summary>
    /// Synchronize position, rotation and transform.parent.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaTransform")]
    public class ReplicaTransform : ReplicaBehaviour, StateInterpolator<ReplicaTransform.State>.IStateAdapter {
        public struct State {
            public float Timestamp;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        StateInterpolator<State> _interpolator;

        public void LerpStates(State oldState, State newState, ref State resultState, float t) {
            if ((oldState.Position - newState.Position).sqrMagnitude < 1) {
                resultState.Position = Vector3.LerpUnclamped(oldState.Position, newState.Position, t);
            } else {
                resultState.Position = newState.Position;
            }
            resultState.Rotation = Quaternion.SlerpUnclamped(oldState.Rotation, newState.Rotation, t);
        }

        public override void Serialize(IBitWriter bs, SerializeContext ctx) {
            bs.WriteVector3(transform.position);
            bs.WriteQuaternion(transform.rotation);
        }

        public override void Deserialize(BitReader bs) {
            var position = bs.ReadVector3();
            var rotation = bs.ReadQuaternion();

            var newState = new State() {
                Timestamp = Time.time,
                Position = position,
                Rotation = rotation
            };
            _interpolator.AddState(newState);
        }

        protected void Awake() {
            _interpolator = new(this);
        }

        protected void Update() {
            if (isClient) {
                var state = new State() {
                    Position = transform.position,
                    Rotation = transform.rotation
                };
                _interpolator.Sample(ref state, Replica.SettingsOrDefault.DesiredUpdateRate * 2.5f);
                transform.position = state.Position;
                transform.rotation = state.Rotation;
            }
        }
    }
}
