using System;

namespace Cube.Transport {
    [Serializable]
    public struct Connection : IEquatable<Connection> {
        public static readonly Connection Invalid = new Connection(unchecked((ulong)-1));

        public ulong id { get; private set; }

        public Connection(ulong id) {
            this.id = id;
        }

        public override string ToString() => id.ToString();

        public override bool Equals(object other) => Equals((Connection)other);

        public override int GetHashCode() => (int)(17 + id * 31);

        public bool Equals(Connection other) => id == other.id;

        public static bool operator ==(Connection lhs, Connection rhs) => lhs.Equals(rhs);
        public static bool operator !=(Connection lhs, Connection rhs) => !lhs.Equals(rhs);
    }
}
