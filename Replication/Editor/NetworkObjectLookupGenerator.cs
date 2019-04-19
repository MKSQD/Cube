using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Cube.Replication {
    public class NetworkObjectLookupGenerator {
        [DidReloadScripts]
        public static void ForceRefreshCode() {
            var networkObjects = new List<NetworkObject>();

            var assetGuids = AssetDatabase.FindAssets("t:NetworkObject");
            foreach (var assetGuid in assetGuids) {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

                var asset = AssetDatabase.LoadAssetAtPath<NetworkObject>(assetPath);
                if (asset == null) {
                    Debug.LogError("LoadAssetAtPath failed (path=" + assetPath + ")");
                    continue;
                }

                networkObjects.Add(asset);
            }

            var lookup = ScriptableObject.CreateInstance<NetworkObjectLookup>();
            lookup.entries = networkObjects.ToArray();

            //
            var newLookupPath = "Assets/Cube/Resources/NetworkObjectLookup.asset";

            var oldLookup = AssetDatabase.LoadAssetAtPath<NetworkObjectLookup>(newLookupPath);
            if (lookup == oldLookup)
                return;
            
            AssetDatabase.CreateAsset(lookup, newLookupPath);
        }
    }
}