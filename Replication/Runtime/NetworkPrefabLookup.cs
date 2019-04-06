using UnityEngine;

namespace Cube.Replication {
    public class NetworkPrefabLookup : ScriptableObject {
        static NetworkPrefabLookup _instance;
        public static NetworkPrefabLookup instance {
            get {
                if (_instance == null) {
                    _instance = Resources.Load<NetworkPrefabLookup>("NetworkPrefabLookup");
                }
                return _instance;
            }
        }

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
