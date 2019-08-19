using System;

namespace Cube.Replication {
    class RingBuffer<T> {
        T[] _data;
        int _firstIdx = 0;
        int _lastIdx = -1;

        public RingBuffer(int capacity) {
            capacity = Math.Max(1, capacity);
            _data = new T[capacity];
        }

        public int Count {
            get {
                if (_lastIdx == -1)
                    return 0;

                var endIdx = (_lastIdx + 1) % _data.Length;
                if (endIdx <= _firstIdx) {
                    endIdx += _data.Length;
                }
                return endIdx - _firstIdx;
            }
        }

        public int Capacity {
            get {
                return _data.Length;
            }
        }

        public bool IsFull {
            get {
                return _lastIdx != -1
                    && ((_lastIdx + 1 + _data.Length) % _data.Length) == _firstIdx;
            }
        }

        public T this[int idx] {
            get {
                return Get(idx);
            }
            set {
                Set(idx, value);
            }
        }

        public void Clear() {
            _firstIdx = 0;
            _lastIdx = -1;
        }

        public void Add(T t) {
            if (IsFull) {
                _firstIdx = (_firstIdx + 1) % _data.Length;
            }
            _lastIdx = (_lastIdx + 1) % _data.Length;

            _data[_lastIdx] = t;
        }

        public T Get(int idx) {
            if (idx < 0 || idx > Count - 1)
                throw new IndexOutOfRangeException();

            return _data[(_firstIdx + idx) % _data.Length];
        }

        public T GetLatest() {
            if (Count == 0)
                throw new IndexOutOfRangeException("RingBuffer empty");

            return Get(Count - 1);
        }

        public T GetOldest() {
            if (Count == 0)
                throw new IndexOutOfRangeException("RingBuffer empty");

            return Get(0);
        }

        public void Set(int idx, T t) {
            if (idx < 0 || idx > Count - 1)
                throw new IndexOutOfRangeException();

            var actualIdx = (_firstIdx + idx) % _data.Length;
            _data[actualIdx] = t;
        }
    }
}