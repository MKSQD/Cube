using UnityEngine;

namespace Cube.Replication {
    public class NetworkObject : ScriptableObject {
        public string networkAssetId;

#if UNITY_EDITOR
        void OnValidate() {
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            var assetGuid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
            networkAssetId = assetGuid;
        }
#endif
    }
}