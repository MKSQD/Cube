using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Replication {
    public class StateExtrapolator<T> {
        public interface IStateAdapter {
            T PredictState(T oldState, T newState, float t);
            void LerpStates(T oldState, T newState, ref T resultState, float t);
        }

        readonly T[] _states;
        readonly float[] _timestamps;
        int _numStates;
        readonly IStateAdapter _adapter;

        public StateExtrapolator(IStateAdapter adapter) {
            _states = new T[2];
            _timestamps = new float[2];
            _adapter = adapter;
        }

        public void AddState(T newState) => AddState(newState, Time.time);

        public void AddState(T newState, float time) {
            for (int i = _states.Length - 1; i >= 1; i--) {
                _states[i] = _states[i - 1];
                _timestamps[i] = _timestamps[i - 1];
            }
            _states[0] = newState;
            _timestamps[0] = time;

            _numStates = Mathf.Min(_numStates + 1, _states.Length);
        }

        public void Sample(ref T resultState, float extrapolationTime) {
            if (_numStates == 0)
                return;

            if (_numStates == 1) {
                resultState = _states[0];
                return;
            }

            var t = Time.time + extrapolationTime;

            // Predict current
            T newPredicted;
            float currentA;
            {
                var actualTimeDiff = Mathf.Max(0, t - _timestamps[0]);
                var timeDiff = _timestamps[0] - _timestamps[1];
                currentA = Mathf.Min(actualTimeDiff / timeDiff, 3);
                newPredicted = _adapter.PredictState(_states[1], _states[0], currentA);
            }

            // Predict old
            T oldPredicted = newPredicted;
            if (_numStates > 2) {
                var actualTimeDiff = t - _timestamps[1];
                var timeDiff = _timestamps[1] - _timestamps[2];
                var a = Mathf.Min(actualTimeDiff / timeDiff, 3);
                oldPredicted = _adapter.PredictState(_states[2], _states[1], a);
            }

            var clampedA = Mathf.Min(currentA, 1);
            _adapter.LerpStates(oldPredicted, newPredicted, ref resultState, clampedA);
        }
    }
}