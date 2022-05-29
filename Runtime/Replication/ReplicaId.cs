using System;
using UnityEngine;

namespace Cube.Replication {
    /// <summary>
    /// RID (Replica ID) uniquely identifieing a single Replica instance globally.
    /// </summary>
    [Serializable]
    public struct ReplicaId {
        public static readonly ReplicaId Invalid = CreateFromExisting(0);

        [SerializeField]
        readonly ushort _data;
        public ushort Data => _data;

        internal ReplicaId(ushort id) => _data = id;

        internal static ReplicaId Create(ServerReplicaManager replicaManager) => new(replicaManager.AllocateLocalReplicaId());
        public static ReplicaId CreateFromExisting(ushort id) => new(id);

        public override bool Equals(object other) {
            if (other == null || GetType() != other.GetType())
                return false;

            return Equals((ReplicaId)other);
        }

        public override string ToString() => _data.ToString();

        public bool Equals(ReplicaId other) => _data == other._data;

        public override int GetHashCode() => _data;

        public static bool operator ==(ReplicaId lhs, ReplicaId rhs) => lhs.Equals(rhs);
        public static bool operator !=(ReplicaId lhs, ReplicaId rhs) => !(lhs == rhs);
    }
}
