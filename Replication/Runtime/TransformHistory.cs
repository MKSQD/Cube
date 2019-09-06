using UnityEngine;

namespace Cube.Replication {
    public class TransformHistory {
        struct Entry {
            public float time;
            public Vector3 position;
            public Quaternion rotation;

            public static Entry Lerp(Entry from, Entry to, float time) {
                if (from.time == to.time)
                    return from;

                var t = (float)((time - from.time) / (double)(to.time - from.time));
                Vector3 pos;
                Quaternion rot;
                var aboveTeleportThreshold = (from.position - to.position).sqrMagnitude > 3;
                if (!aboveTeleportThreshold) {
                    pos = Vector3.Lerp(from.position, to.position, t);
                    rot = Quaternion.Slerp(from.rotation, to.rotation, t);
                }
                else {
                    pos = to.position;
                    rot = to.rotation;
                }
                return new Entry() {
                    time = time,
                    position = pos,
                    rotation = rot
                };
            }

            public static Entry GetTransformAtTime(RingBuffer<Entry> history, float desiredTime) {
                for (var i = history.Count - 1; i > 0; i--) {
                    var entry1 = history.Get(i);
                    var entry2 = history.Get(i - 1);
                    if (entry1.time >= desiredTime && entry2.time < desiredTime)
                        return Lerp(entry2, entry1, desiredTime);
                }

                if (history.Count > 0)
                    return history.GetLatest();

                return new Entry() {
                    time = desiredTime,
                    position = Vector3.zero,
                    rotation = Quaternion.identity
                };
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
