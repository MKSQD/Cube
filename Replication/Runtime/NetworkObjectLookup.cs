using System;
using UnityEngine;

namespace Cube.Replication {
    /// <summary>
    /// Used for runtime lookup of NetworkObject derived ScriptableObject instances in the Assets folder.
    /// </summary>
    [Serializable]
    public class NetworkObjectLookup : ScriptableObject, IEquatable<NetworkObjectLookup> {
        static NetworkObjectLookup instance;
        public static NetworkObjectLookup Instance {
            get {
                if (instance == null) {
                    instance = Resources.Load<NetworkObjectLookup>("NetworkObjectLookup");
                }
                return instance;
            }
        }

        public NetworkObject[] entries;

        public NetworkObject CreateFromNetworkAssetId(int networkAssetId) {
            if (networkAssetId == -1)
                return null;

            return entries[networkAssetId];
        }

        public override bool Equals(object other) {
            return Equals(other as NetworkObjectLookup);
        }

        public override int GetHashCode() {
            return entries.GetHashCode();
        }

        public bool Equals(NetworkObjectLookup other) {
            if (entries.Length != other.entries.Length)
                return false;

            for (int i = 0; i < entries.Length; ++i) {
                if (entries[i].networkAssetId != other.entries[i].networkAssetId)
                    return false;
            }

            return true;
        }

        public static bool operator ==(NetworkObjectLookup lhs, NetworkObjectLookup rhs) {
            return Equals(lhs, rhs);
        }

        public static bool operator !=(NetworkObjectLookup lhs, NetworkObjectLookup rhs) {
            return !(lhs == rhs);
        }
    }
}
