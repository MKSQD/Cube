using System;
using UnityEngine;

namespace Cube {
    [Serializable]
    struct IntVector3 : IEquatable<IntVector3> {
        public static readonly IntVector3 zero = new IntVector3(0, 0, 0);

        public int x, y, z;

        public IntVector3(int x, int y, int z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public IntVector3(Vector3 vec) {
            x = (int)vec.x;
            y = (int)vec.y;
            z = (int)vec.z;
        }

        public float magnitude {
            get { return Mathf.Sqrt(sqrMagnitude); }
        }

        public int sqrMagnitude {
            get { return x * x + y * y + z * z; }
        }

        public override bool Equals(object other) {
            return Equals((IntVector3)other);
        }

        public bool Equals(IntVector3 other) {
            return x == other.x && y == other.y && z == other.z;
        }

        public static bool operator ==(IntVector3 a, IntVector3 b) {
            return a.Equals(b);
        }

        public static bool operator !=(IntVector3 a, IntVector3 b) {
            return !(a == b);
        }

        public override int GetHashCode() {
            // https://stackoverflow.com/questions/5928725/hashing-2d-3d-and-nd-vectors
            return ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) % 1000;
        }

        public override string ToString() {
            return string.Format("({0},{1},{2})", x, y, z);
        }

        public static implicit operator Vector3(IntVector3 vec) {
            return new Vector3(vec.x, vec.y, vec.z);
        }

        public static IntVector3 operator +(IntVector3 vec1, IntVector3 vec2) {
            return new IntVector3(vec1.x + vec2.x, vec1.y + vec2.y, vec1.z + vec2.z);
        }

        public static IntVector3 operator -(IntVector3 vec1, IntVector3 vec2) {
            return new IntVector3(vec1.x - vec2.x, vec1.y - vec2.y, vec1.z - vec2.z);
        }

        public static IntVector3 operator *(IntVector3 vec1, int scalar) {
            return new IntVector3(vec1.x * scalar, vec1.y * scalar, vec1.z * scalar);
        }

        public static Vector3 operator *(IntVector3 vec1, float scalar) {
            return new Vector3(vec1.x * scalar, vec1.y * scalar, vec1.z * scalar);
        }
    }
}