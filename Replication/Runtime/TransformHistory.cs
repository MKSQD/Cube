using UnityEngine;

namespace Cube.Replication {
    public class TransformHistory {
        public int Capacity => _history.Capacity;

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
                if (history.Count == 0)
                    return new Entry() {
                        time = desiredTime,
                        position = Vector3.zero,
                        rotation = Quaternion.identity
                    };

                if (desiredTime <= history.GetOldest().time)
                    return history.GetOldest();

                for (var i = history.Count - 1; i > 0; i--) {
                    var entry1 = history.Get(i);
                    var entry2 = history.Get(i - 1);
                    if (entry1.time >= desiredTime && entry2.time < desiredTime)
                        return Lerp(entry2, entry1, desiredTime);
                }

                return history.GetLatest();
            }
        }

        RingBuffer<Entry> _history;
        float _writeInterval;

        public TransformHistory(float maxWriteIntervalMs, float maxHistoryTimeMs) {
            _writeInterval = maxWriteIntervalMs * 0.001f;
            _history = new RingBuffer<Entry>((int)(maxHistoryTimeMs / maxWriteIntervalMs) + 1);
        }

        public TransformHistory(int capacity) {
            _writeInterval = 0.001f;
            _history = new RingBuffer<Entry>(capacity);
        }

        public void Add(float timestamp, Pose curPose) {
            if (_history.Count > 0) {
                var latest = _history.GetLatest();
                timestamp -= latest.time;

                if (timestamp < _writeInterval)
                    return;

                var a = (int)(timestamp / _writeInterval);
                timestamp = a * _writeInterval;

                var currentTransform = new Entry() {
                    time = latest.time + timestamp,
                    position = curPose.position,
                    rotation = curPose.rotation,
                };
                _history.Add(currentTransform);
            }
            else {
                var currentTransform = new Entry() {
                    time = timestamp,
                    position = curPose.position,
                    rotation = curPose.rotation,
                };
                _history.Add(currentTransform);
            }
        }

        public void Sample(float timestamp, out Vector3 delayedPos, out Quaternion delayedRot) {
            var desiredTransform = Entry.GetTransformAtTime(_history, timestamp);
            delayedPos = desiredTransform.position;
            delayedRot = desiredTransform.rotation;
        }
    }
}
