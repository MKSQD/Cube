﻿using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Replication {
    /// <summary>
    /// Used for runtime lookup of NetworkObject instances in the Assets folder.
    /// </summary>
    public class NetworkObjectLookup : GlobalData<NetworkObjectLookup> {
        public NetworkObject[] Entries;

        public NetworkObject CreateFromNetworkAssetId(int networkAssetId) {
            Assert.IsTrue(networkAssetId != -1);
            return Entries[networkAssetId];
        }
    }
}
