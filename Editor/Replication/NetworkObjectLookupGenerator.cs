using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Cube.Replication {
    public class NetworkObjectLookupGenerator : AssetPostprocessor {
        [MenuItem("Tools/Cube/Internal/Force refresh NetworkObjectLookup")]
        static void Force() {
            Generate();
            Debug.Log("Done");
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            var found = false;

            var assetLists = new string[][] { importedAssets, deletedAssets };
            foreach (var assetList in assetLists) {
                if (found)
                    break;

                foreach (var assetPath in assetList) {
                    if (assetPath.EndsWith(".asset", StringComparison.InvariantCultureIgnoreCase)) {
                        var asset = AssetDatabase.LoadAssetAtPath<NetworkObject>(assetPath);
                        if (asset != null) {
                            found = true;
                            break;
                        }
                    }
                }
            }

            if (found) {
                Generate();
            }
        }

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
                    Debug.LogWarning("LoadAssetAtPath failed (path=" + assetPath + ")");
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
            if (!newEntries.SequenceEqual(lookup.Entries)) {
                lookup.Entries = newEntries;
                EditorUtility.SetDirty(lookup);
            }
        }
    }
}