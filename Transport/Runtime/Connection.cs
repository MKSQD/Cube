using System;

namespace Cube.Transport {
    [Serializable]
    public struct Connection : IEquatable<Connection> {
        public static readonly Connection Invalid = new Connection(unchecked((ulong)-1));
        
        public ulong id {
            get;
            internal set;
        }

        public Connection(ulong id) {
            this.id = id;
        }

        public override string ToString() {
            return id.ToString();
        }

        public override bool Equals(object other) {
            return Equals((Connection)other);
        }

        public override int GetHashCode() {
            return 17 + id * 31;
        }

        public bool Equals(Connection other) {
            return id == other.id;
        }

        public static bool operator ==(Connection lhs, Connection rhs) {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Connection lhs, Connection rhs) {
            return !lhs.Equals(rhs);
        }
    }
}
