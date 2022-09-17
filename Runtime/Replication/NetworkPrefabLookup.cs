using UnityEngine;

namespace Cube.Replication {
    public class NetworkPrefabLookup : GlobalData<NetworkPrefabLookup> {
        public GameObject[] Prefabs;
        public ushort[] Hashes;

        public bool TryGetClientPrefabForIndex(int prefabIdx, out GameObject prefab) {
            if (prefabIdx == 0 || prefabIdx > Prefabs.Length) {
                prefab = null;
                return false;
            }
            prefab = Prefabs[prefabIdx];
            return true;
        }

        public ushort GetIndexForHash(ushort hash) {
            int minNum = 0;
            int maxNum = Hashes.Length - 1;

            while (minNum <= maxNum) {
                var mid = (minNum + maxNum) / 2;

                var midHash = Hashes[mid];
                if (hash == midHash) {
                    return (ushort)mid;
                } else if (hash < midHash) {
                    maxNum = mid - 1;
                } else {
                    minNum = mid + 1;
                }
            }

            throw new System.Exception("Hash not found");
        }
    }
}
