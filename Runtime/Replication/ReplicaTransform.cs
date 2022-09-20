using Cube.Transport;
using UnityEngine;


namespace Cube.Replication {
    /// <summary>
    /// Synchronize position, rotation and transform.parent.
    /// </summary>
    /// <remarks>Available in: Editor/Client/Server</remarks>
    [AddComponentMenu("Cube/ReplicaTransform")]
    public sealed class ReplicaTransform : ReplicaBehaviour, StateInterpolator<ReplicaTransform.State>.IStateAdapter {
        public struct State {
            public float Timestamp;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public WorldBoundsSettings WorldBounds;

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
            bs.WriteLossyFloat(transform.position.x, WorldBounds.MinWorldX, WorldBounds.MaxWorldX, 0.01f);
            bs.WriteLossyFloat(transform.position.y, WorldBounds.MinWorldY, WorldBounds.MaxWorldY, 0.01f);
            bs.WriteLossyFloat(transform.position.z, WorldBounds.MinWorldZ, WorldBounds.MaxWorldZ, 0.01f);
            bs.WriteQuaternion(transform.rotation);
        }

        public override void Deserialize(BitReader bs) {
            Vector3 pos;
            pos.x = bs.ReadLossyFloat(WorldBounds.MinWorldX, WorldBounds.MaxWorldX, 0.01f);
            pos.y = bs.ReadLossyFloat(WorldBounds.MinWorldY, WorldBounds.MaxWorldY, 0.01f);
            pos.z = bs.ReadLossyFloat(WorldBounds.MinWorldZ, WorldBounds.MaxWorldZ, 0.01f);
            var rotation = bs.ReadQuaternion();

            var newState = new State() {
                Timestamp = Time.time,
                Position = pos,
                Rotation = rotation
            };
            _interpolator.AddState(newState);
        }

        void Awake() {
            _interpolator = new(this, Replica.SettingsOrDefault.DesiredUpdateRate * 2.5f, Replica.SettingsOrDefault.DesiredUpdateRate);
        }

        void Update() {
            if (isClient) {
                var state = new State() {
                    Position = transform.position,
                    Rotation = transform.rotation
                };
                _interpolator.Sample(ref state, Replica.SettingsOrDefault.DesiredUpdateRate * 1.5f);
                transform.position = state.Position;
                transform.rotation = state.Rotation;
            }
        }
    }
}
