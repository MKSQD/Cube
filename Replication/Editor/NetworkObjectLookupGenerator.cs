using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Cube.Replication {
    public class NetworkObjectLookupGenerator {
        [MenuItem("Cube/Internal/Force refresh NetworkObjectLookup")]
        static void Force() {
            Generate();
            Debug.Log("Done");
        }

        [DidReloadScripts]
        public static void Generate() {
            if (BuildPipeline.isBuildingPlayer)
                return; // No need to regenerate the data while building

            var networkObjects = new List<NetworkObject>();
            var refs = new List<AssetReference>();

            var assetGuids = AssetDatabase.FindAssets("t:NetworkObject");
            foreach (var assetGuid in assetGuids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

                var asset = AssetDatabase.LoadAssetAtPath<NetworkObject>(assetPath);
                if (asset == null) {
                    Debug.LogError("LoadAssetAtPath failed (path=" + assetPath + ")");
                    continue;
                }

                if (asset.networkAssetId != networkObjects.Count) {
                    asset.networkAssetId = networkObjects.Count;
                    EditorUtility.SetDirty(asset);
                }

                networkObjects.Add(asset);
                refs.Add(new AssetReference(assetGuid));
            }

            var newEntries = networkObjects.ToArray();

            var lookup = NetworkObjectLookup.Instance;

            if (!newEntries.Equals(lookup.Entries)) {
                lookup.Entries = newEntries;

                EditorUtility.SetDirty(lookup);
            }
        }
    }
}