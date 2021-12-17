using System;
using UnityEngine;

namespace Cube.Replication {
    [Serializable]
    public struct ReplicaId {
        public static readonly ReplicaId Invalid = CreateFromExisting(0);

        [SerializeField]
        ushort data;
        public ushort Data {
            get { return data; }
        }

        internal ReplicaId(ushort id) {
            data = id;
        }

        internal static ReplicaId Create(ServerReplicaManager replicaManager) {
            return new ReplicaId(replicaManager.AllocateLocalReplicaId());
        }

        public static ReplicaId CreateFromExisting(ushort id) {
            return new ReplicaId(id);
        }

        public override bool Equals(object other) {
            if (other == null || GetType() != other.GetType())
                return false;

            return Equals((ReplicaId)other);
        }

        public override string ToString() {
            return data.ToString();
        }

        public bool Equals(ReplicaId other) {
            return data == other.data;
        }

        public override int GetHashCode() {
            return data;
        }

        public static bool operator ==(ReplicaId lhs, ReplicaId rhs) {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ReplicaId lhs, ReplicaId rhs) {
            return !(lhs == rhs);
        }
    }
}
