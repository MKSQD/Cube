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
                    var j = Math.Min(i + 1, _entries.Count - 1);

                    var oldEntry = _entries[i];
                    var newEntry = _entries[j];

                    var r = newEntry.time - oldEntry.time;
                    if (r > 0.001f && snapDistance > (oldEntry.position - newEntry.position).sqrMagnitude) {
                        var a = (whenSec - oldEntry.time) / r;
                        a = Mathf.Min(a, 1);

                        currentPosition = Hermite(oldEntry.position, newEntry.position, oldEntry.velocity * r, newEntry.velocity * r, a);
                        currentRotation = Quaternion.SlerpUnclamped(oldEntry.rotation, newEntry.rotation, a);
                    }
                    else {
                        currentPosition = newEntry.position;
                        currentRotation = newEntry.rotation;
                    }

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
    }
}
