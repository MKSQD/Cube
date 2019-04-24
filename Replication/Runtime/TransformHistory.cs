using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

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

        public float snapDistance = 8;

        List<Entry> _entries = new List<Entry>();
        float _maxHistoryTimeSec;

        public TransformHistory(float maxHistoryTimeSec) {
            _maxHistoryTimeSec = maxHistoryTimeSec;
        }

        public void Write(float timeSec, Vector3 position, Vector3 velocity, Quaternion rotation) {
            while (_entries.Count > 0 && _entries.First().time < timeSec - _maxHistoryTimeSec) {
                _entries.RemoveAt(0);
            }

            _entries.Add(new Entry() {
                time = timeSec,
                position = position,
                velocity = velocity,
                rotation = rotation
            });

            //Debug.Log("Write [" + string.Join("  ", _entries.Select(e2 => e2.time)) + "]");
        }

        public ReadResult Read(float whenSec, ref Vector3 currentPosition, ref Quaternion currentRotation) {
            int k = 0;
            for (int i = _entries.Count - 1; i >= 0; --i) {
                if (whenSec >= _entries[i].time) {
                    k = i;
                    break;
                }
            }

            for (int i = 0; i < _entries.Count; ++i) {
                var j = Math.Min(i + 1, _entries.Count - 1);

                if (i != k) {
                    Debug.DrawLine(_entries[i].position, _entries[j].position, Color.red);
                    Debug.DrawLine(_entries[i].position - new Vector3(0.1f, 0, 0), _entries[i].position + new Vector3(0.1f, 0, 0), Color.red);
                    Debug.DrawLine(_entries[i].position - new Vector3(0, 0.1f, 0), _entries[i].position + new Vector3(0, 0.1f, 0), Color.red);
                }
                else {
                    Debug.DrawLine(_entries[i].position, _entries[j].position, Color.green);
                    Debug.DrawLine(_entries[i].position - new Vector3(0.1f, 0, 0), _entries[i].position + new Vector3(0.1f, 0, 0), Color.red);
                    Debug.DrawLine(_entries[i].position - new Vector3(0, 0.1f, 0), _entries[i].position + new Vector3(0, 0.1f, 0), Color.red);
                }
            }

            for (int i = _entries.Count - 1; i >= 0; --i) {
                if (whenSec >= _entries[i].time) {
                    var lastEntry2 = _entries[Math.Max(i - 1, 0)];
                    var lastEntry = _entries[i];
                    var nextEntry = _entries[Math.Min(i + 1, _entries.Count - 1)];
                    var nextEntry2 = _entries[Math.Min(i + 2, _entries.Count - 1)];

                    var r = nextEntry.time - lastEntry.time;
                    var t = r > 0.001f ? (whenSec - lastEntry.time) / r : 1;
                    currentPosition = CubicHermite(lastEntry2.position, lastEntry.position, nextEntry.position, nextEntry2.position, t);
                    currentRotation = Quaternion.Slerp(lastEntry.rotation, nextEntry.rotation, t);
                    
                    //Debug.Log("Read " + whenSec + " [" + i + ":" + oldEntry.time + "  " + j + ":" + newEntry.time + "] a=" + a);
                    return ReadResult.Interpolated;
                }
            }

            return ReadResult.None;
        }

        static Vector3 Hermite(Vector3 p1, Vector3 p2, Vector3 v1, Vector3 v2, float t) {
            var t2 = Mathf.Pow(t, 2);
            var t3 = Mathf.Pow(t, 3);

            var a = 1 - 3 * t2 + 2 * t3;
            var b = t2 * (3 - 2 * t);
            var c = t * Mathf.Pow(t - 1, 2);
            var d = t2 * (t - 1);

            return a * p1 + b * p2 + c * v1 + d * v2;
        }

        static Vector3 CubicHermite(Vector3 A, Vector3 B, Vector3 C, Vector3 D, float t) {
            var a = -A / 2.0f + (3.0f * B) / 2.0f - (3.0f * C) / 2.0f + D / 2.0f;
            var b = A - (5.0f * B) / 2.0f + 2.0f * C - D / 2.0f;
            var c = -A / 2.0f + C / 2.0f;
            var d = B;

            return a * t * t * t + b * t * t + c * t + d;
        }
    }
}
