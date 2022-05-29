using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Replication {
    public class NetworkPrefabLookup : GlobalData<NetworkPrefabLookup> {
        public GameObject[] Prefabs;
        public ushort[] Hashes;

        public bool TryGetClientPrefabForIndex(int prefabIdx, out GameObject prefab) {
            if (prefabIdx == 0) {
                prefab = null;
                return false;
            }
            if (prefabIdx > Prefabs.Length) {
                prefab = null;
                return false;
            }
            prefab = Prefabs[prefabIdx];
            return true;
        }

        public ushort GetIndexForHash(ushort hash) {
            Assert.IsTrue(Hashes.Length < ushort.MaxValue);


            int minNum = 0;
            int maxNum = Hashes.Length - 1;

            while (minNum <= maxNum) {
                int mid = (minNum + maxNum) / 2;
                if (hash == Hashes[mid]) {
                    return (ushort)mid;
                } else if (hash < Hashes[mid]) {
                    maxNum = mid - 1;
                } else {
                    minNum = mid + 1;
                }
            }




            // for (ushort i = 0; i < Hashes.Length; ++i) {
            //     if (Hashes[i] == hash)
            //         return i;
            // }
            throw new System.Exception("Hash not found");
        }
    }
}
