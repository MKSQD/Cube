using UnityEngine;

namespace Cube.Replication {
    public class NetworkPrefabLookup : GlobalData<NetworkPrefabLookup> {
        public GameObject[] Prefabs;

        public bool TryGetClientPrefabForIndex(int prefabIdx, out GameObject prefab) {
            if (prefabIdx == 0) {
                prefab = null;
                return false;
            }
            if (prefabIdx > Prefabs.Length) {
                prefab = null;
                return false;
            }
            prefab = Prefabs[prefabIdx - 1];
            return true;
        }
    }
}
