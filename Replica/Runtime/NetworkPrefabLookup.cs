using UnityEngine;

namespace Cube.Networking.Replicas {
    public class NetworkPrefabLookup : ScriptableObject {
        public static NetworkPrefabLookup instance { get { return null; } }

        public GameObject[] prefabs;

        public bool TryGetClientPrefabForIndex(int prefabIdx, out GameObject prefab) {
            if (prefabIdx >= prefabs.Length) {
                prefab = null;
                return false;
            }
            prefab = prefabs[prefabIdx];
            return true;
        }
    }
}
