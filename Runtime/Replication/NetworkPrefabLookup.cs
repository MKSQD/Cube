using UnityEngine;

namespace Cube.Replication {
    public class NetworkPrefabLookup : GlobalData<NetworkPrefabLookup> {
        public GameObject[] Prefabs;

        public bool TryGetClientPrefabForIndex(int prefabIdx, out GameObject prefab) {
            if (prefabIdx >= Prefabs.Length) {
                prefab = null;
                return false;
            }
            prefab = Prefabs[prefabIdx];
            return true;
        }
    }
}
