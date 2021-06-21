using UnityEngine.AddressableAssets;

namespace Cube.Replication {
    public class NetworkPrefabLookup : GlobalData<NetworkPrefabLookup> {
        public AssetReferenceGameObject[] Prefabs;

        public bool TryGetClientPrefabForIndex(int prefabIdx, out AssetReferenceGameObject prefab) {
            if (prefabIdx >= Prefabs.Length) {
                prefab = null;
                return false;
            }
            prefab = Prefabs[prefabIdx];
            return true;
        }
    }
}
