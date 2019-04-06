using System;
using UnityEngine;

namespace Cube.Replication {
    /// <summary>
    /// Used for runtime lookup of NetworkObject derived ScriptableObject instances in the Assets folder.
    /// </summary>
    public class NetworkObjectLookup : ScriptableObject, IEquatable<NetworkObjectLookup> {
        public static NetworkObjectLookup instance { get { return null; } }

        public NetworkObject[] entries;

        public NetworkObject CreateFromNetworkAssetId(string networkAssetId) {
            foreach (var entry in entries) {
                if (entry.networkAssetId == networkAssetId)
                    return entry;
            }
            return null;
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
                if (entries[i] != other.entries[i])
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
