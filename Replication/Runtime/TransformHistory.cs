using UnityEngine;

namespace Cube.Replication {
    public class TransformHistory {
        struct Entry {
            public float time;
            public Vector3 position;
            public Quaternion rotation;

            public static Entry Lerp(Entry from, Entry to, float time) {
                if (from.time == to.time) {
                    return from;
                }
                float fraction = (float)(((double)(time - from.time)) / ((double)(to.time - from.time)));
                return new Entry() {
                    time = time,
                    position = Vector3.Lerp(from.position, to.position, fraction),
                    rotation = Quaternion.Slerp(from.rotation, to.rotation, fraction)
                };
            }

            public static Entry GetTransformAtTime(RingBuffer<Entry> history, float desiredTime) {
                for (int i = history.Count - 1; i > 0; i--) {
                    if (history.Get(i).time >= desiredTime && history.Get(i - 1).time < desiredTime) {
                        return Lerp(history.Get(i - 1), history.Get(i), desiredTime);
                    }
                }

                if (history.Count > 0) {
                    return history.GetLatest();
                }
                else {
                    // No history data available.
                    return new Entry() {
                        time = desiredTime,
                        position = Vector3.zero,
                        rotation = Quaternion.identity
                    };
                }
            }
        }

        RingBuffer<Entry> _history;

        public TransformHistory(int capacity = 32) {
            _history = new RingBuffer<Entry>(capacity);
        }

        public void Add(Pose curPose, float timestamp) {
            var currentTransform = new Entry() {
                  time = timestamp,
                  position = curPose.position,
                  rotation = curPose.rotation,
              };

            _history.Add(currentTransform);
        }

        public void Sample(float timestamp, out Vector3 delayedPos, out Quaternion delayedRot) {
            Entry desiredTransform = Entry.GetTransformAtTime(_history, timestamp);
            delayedPos = desiredTransform.position;
            delayedRot = desiredTransform.rotation;
        }
    }
}
