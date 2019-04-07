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

        public float snapDistance = 3;

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

        public ReadResult Read(float whenSec, ref Vector3 currentPosition, ref Vector3 currentVelocity, ref Quaternion currentRotation) {
            for (int i = _entries.Count - 1; i >= 0; --i) {
                if (whenSec >= _entries[i].time) {
                    var j = Math.Min(i + 1, _entries.Count - 1);

                    var e1 = _entries[i];
                    var e2 = _entries[j];

                    var r = Mathf.Max(e2.time - e1.time, 0.001f);
                    var a = Mathf.Min((whenSec - e1.time) / r, 1);

                    var lerp = (e1.position - e2.position).sqrMagnitude;
                    if (lerp < snapDistance) {
                        currentPosition = Vector3.LerpUnclamped(e1.position, e2.position, a);
                        currentVelocity = Vector3.LerpUnclamped(e1.velocity, e2.velocity, a);
                        currentRotation = Quaternion.SlerpUnclamped(e1.rotation, e2.rotation, a);
                    }
                    else {
                        currentPosition = e2.position;
                        currentVelocity = e2.velocity;
                        currentRotation = e2.rotation;
                    }

                    //Debug.Log("Read " + whenSec + " [" + i + ":" + e1.time + "  " + j + ":" + e2.time + "] a=" + a);
                    return ReadResult.Interpolated;
                }
            }

            return ReadResult.None;
        }
    }
}
