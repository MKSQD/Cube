using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Replication {
    public class StateInterpolator<T> {
        public interface IStateAdapter {
            void LerpStates(T oldState, T newState, ref T resultState, float t);
        }

        readonly T[] _states;
        readonly float[] _timestamps;
        int _numStates;
        readonly IStateAdapter _adapter;
        readonly float _addStateRate;

        public StateInterpolator(IStateAdapter adapter, float maxInterpolationTime, float addStateRate) {
            var num = Mathf.CeilToInt(maxInterpolationTime / addStateRate);
            Assert.IsTrue(num >= 1);

            _states = new T[num];
            _timestamps = new float[num];
            _adapter = adapter;
            _addStateRate = addStateRate;
        }

        public void AddState(T newState) => AddState(newState, Time.time);

        public void AddState(T newState, float time) {
            if (_numStates > 0) {
                var timeSinceLastState = time - _timestamps[0];
                if (timeSinceLastState < _addStateRate)
                    return;
            }

            for (int i = _states.Length - 1; i >= 1; i--) {
                _states[i] = _states[i - 1];
                _timestamps[i] = _timestamps[i - 1];
            }
            _states[0] = newState;
            _timestamps[0] = time;

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
                if (t < _timestamps[stateIdx])
                    continue;

                var length = _timestamps[stateIdx - 1] - _timestamps[stateIdx];
                float a = 0f;
                if (length > 0.0001) {
                    a = (float)((t - _timestamps[stateIdx]) / length);
                    a = Mathf.Max(a, 0);
                }

                _adapter.LerpStates(_states[stateIdx], _states[stateIdx - 1], ref resultState, a);
                return;
            }

            // The requested time is *older* than any state we have, show oldest
            resultState = _states[_numStates - 1];
        }
    }
}