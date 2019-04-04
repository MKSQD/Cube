using UnityEngine;

namespace Cube.Networking.Replicas {
    // #todo this should be in Cube.Networking, but ReplicaBehaviour needs it for rpcs
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