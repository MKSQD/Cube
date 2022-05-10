using UnityEngine;

namespace Cube.Replication {
    public class StateExtrapolator<T> {
        public interface IStateAdapter {
            float GetStateTimestamp(T state);
            Vector3 GetStatePosition(T state);
            void SetStatePosition(ref T state, Vector3 position);
        }

        readonly T[] _states;
        int _numStates;
        readonly IStateAdapter _adapter;

        public StateExtrapolator(IStateAdapter adapter) {
            _states = new T[2];
            _adapter = adapter;
        }

        public void AddState(T newState) {
            for (int i = _states.Length - 1; i >= 1; i--) {
                _states[i] = _states[i - 1];
            }
            _states[0] = newState;

            _numStates = Mathf.Min(_numStates + 1, _states.Length);
        }

        public void Sample(ref T resultState, float extrapolationTime) {
            if (_numStates == 0)
                return;

            if (_numStates == 1) {
                resultState = _states[0];
                return;
            }

            var t = Time.timeAsDouble + extrapolationTime;

            // Predict current
            Vector3 currentPredicted;
            float currentA;
            {
                var actualTimeDiff = Mathf.Max(0, (float)(t - _adapter.GetStateTimestamp(_states[0])));
                var posDiff = _adapter.GetStatePosition(_states[0]) - _adapter.GetStatePosition(_states[1]);
                var timeDiff = (float)(_adapter.GetStateTimestamp(_states[0]) - _adapter.GetStateTimestamp(_states[1]));
                var a = Mathf.Min(actualTimeDiff / timeDiff, 3);
                currentPredicted = _adapter.GetStatePosition(_states[0]) + posDiff * (float)a;

                currentA = (float)a;
            }

            // Predict old
            Vector3 prevPredicted = currentPredicted;
            if (_numStates > 2) {
                var actualTimeDiff = (float)(t - _adapter.GetStateTimestamp(_states[1]));
                var posDiff = _adapter.GetStatePosition(_states[1]) - _adapter.GetStatePosition(_states[2]);
                var timeDiff = (float)(_adapter.GetStateTimestamp(_states[1]) - _adapter.GetStateTimestamp(_states[2]));
                var a = Mathf.Min(actualTimeDiff / timeDiff, 3);
                prevPredicted = _adapter.GetStatePosition(_states[1]) + posDiff * (float)a;
            }

            var extrapolatedPos = Vector3.Lerp(prevPredicted, currentPredicted, currentA);

            resultState = _states[0];
            _adapter.SetStatePosition(ref resultState, extrapolatedPos);
        }
    }
}