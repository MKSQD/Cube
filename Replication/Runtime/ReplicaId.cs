using System;
using UnityEngine;

namespace Cube.Replication {
    [Serializable]
    public struct ReplicaId {
        public static readonly ReplicaId Invalid = CreateFromExisting(0);

        [SerializeField]
        ushort _data;
        public ushort data {
            get { return _data; }
        }

        internal ReplicaId(ushort id) {
            _data = id;
        }

#if SERVER
        internal static ReplicaId Create(ServerReplicaManager replicaManager) {
            return new ReplicaId(replicaManager.AllocateLocalReplicaId());
        }
#endif

        public static ReplicaId CreateFromExisting(ushort id) {
            return new ReplicaId(id);
        }

        public override bool Equals(object other) {
            if (other == null || GetType() != other.GetType())
                return false;

            return Equals((ReplicaId)other);
        }

        public override string ToString() {
            return _data.ToString();
        }

        public bool Equals(ReplicaId other) {
            return _data == other._data;
        }

        public override int GetHashCode() {
            return _data;
        }

        public static bool operator ==(ReplicaId lhs, ReplicaId rhs) {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ReplicaId lhs, ReplicaId rhs) {
            return !(lhs == rhs);
        }
    }
}
