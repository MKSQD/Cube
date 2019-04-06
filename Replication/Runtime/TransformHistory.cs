using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Cube.Replication {
    public class TransformHistory {
        public enum ReadResult {
            None,
            Interpolated
        }

        struct Entry {
            public float time;
            public Vector3 position;
            public Vector3 velocity;
            public Quaternion rotation;
        }

        List<Entry> _entries;
        float _writeInterval;

        public TransformHistory(float maxWriteInterval, float maxHistoryTime) {
            _writeInterval = maxWriteInterval;
            _entries = new List<Entry>(Mathf.CeilToInt(maxHistoryTime / maxWriteInterval) + 1);
        }

        public void Write(float time, Vector3 position, Vector3 velocity, Quaternion rotation) {
            var entry = new Entry() {
                time = time,
                position = position,
                velocity = velocity,
                rotation = rotation
            };

            if (_entries.Count != 0) {
                if (time - _entries[_entries.Count - 1].time >= _writeInterval) {
                    if (_entries.Count == _entries.Capacity) {
                        _entries.RemoveAt(0);
                    }
                    _entries.Add(entry);
                }
            } else {
                _entries.Add(entry);
            }
        }

        public ReadResult Read(float when, ref Vector3 currentPosition, ref Vector3 currentVelocity, ref Quaternion currentRotation) {
            for (int i = 0; i < _entries.Count - 1; ++i) {
                var p = _entries[i].position;
                Debug.DrawLine(p, _entries[i + 1].position, Color.green);
                Debug.DrawLine(p - Vector3.left * 0.2f, p + Vector3.right * 0.2f, Color.red);
                Debug.DrawLine(p - Vector3.forward * 0.2f, p + Vector3.back * 0.2f, Color.red);
            }

            if (_entries.Count == 0)
                return ReadResult.None;

            var lastEntry = _entries.Last();
            if (when < lastEntry.time) {
                for (var i = _entries.Count - 1; i >= 0; --i) {
                    var C1 = _entries[i];
                    var C2 = _entries[Mathf.Min(i + 1, _entries.Count - 1)];

                    if (when > C1.time) {
                        var divisor = C2.time - C1.time;
                        var lerp = 0f;
                        if (divisor > 0f) {
                            lerp = (when - C1.time) / divisor;
                        }

                        var targetPosition = Vector3.Lerp(C1.position, C2.position, lerp);
                        var targetVelocity = Vector3.Lerp(C1.velocity, C2.velocity, lerp);
                        var targetRotation = Quaternion.Slerp(C1.rotation, C2.rotation, lerp);

                        currentPosition = targetPosition;
                        currentVelocity = targetVelocity;
                        currentRotation = targetRotation;
                    }
                }

                return ReadResult.Interpolated;
            } else {
                currentPosition = lastEntry.position;
                currentVelocity = lastEntry.velocity;
                currentRotation = lastEntry.rotation;

                return ReadResult.None;
            }
        }
    }
}
