using UnityEngine;

namespace Cube.Replication {
    public class StateInterpolator<T> {
        public interface IStateAdapter {
            float GetStateTimestamp(T state);
            void LerpStates(T oldState, T newState, ref T resultState, float t);
        }

        readonly T[] _states;
        int _numStates;
        readonly IStateAdapter _adapter;

        public StateInterpolator(IStateAdapter adapter) {
            _states = new T[10];
            _adapter = adapter;
        }

        public void AddState(T newState) {
            for (int i = _states.Length - 1; i >= 1; i--) {
                _states[i] = _states[i - 1];
            }
            _states[0] = newState;

            _numStates = Mathf.Min(_numStates + 1, _states.Length);
        }

        public void Sample(ref T resultState, float interpolationTime) {
            if (_numStates == 0)
                return;

            if (_numStates == 1) {
                resultState = _states[0];
                return;
            }

            var t = Time.timeAsDouble - interpolationTime;
            for (int stateIdx = 1; stateIdx < _numStates; ++stateIdx) {
                if (t >= _adapter.GetStateTimestamp(_states[stateIdx])) {
                    var newState = _states[stateIdx - 1];
                    var oldState = _states[stateIdx];

                    double length = _adapter.GetStateTimestamp(newState) - _adapter.GetStateTimestamp(oldState);
                    float a = 0f;
                    if (length > 0.0001) {
                        a = (float)((t - _adapter.GetStateTimestamp(oldState)) / length);
                        a = Mathf.Max(a, 0);
                    }

                    _adapter.LerpStates(oldState, newState, ref resultState, a);
                    return;
                }
            }

            // The requested time is *older* than any state we have, show oldest
            resultState = _states[_numStates - 1];
        }
    }
}