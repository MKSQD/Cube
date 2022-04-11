using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Cube.Replication.Editor {

    public class NetworkPrefabLookupGenerator : AssetPostprocessor {
        const string CLIENT_PREFAB_PREFIX = "Client_";
        const string SERVER_PREFAB_PREFIX = "Server_";

        static bool IsReplica(string assetPath, out GameObject gameObject) {
            gameObject = null;

            if (!assetPath.EndsWith(".prefab"))
                return false;

            gameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (gameObject == null)
                return false;

            if (gameObject.GetComponent<Replica>() == null)
                return false;

            return true;
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            var hasChanged = false;
            var prefabs = NetworkPrefabLookup.Instance.Prefabs.ToList();

            foreach (var importedAsset in importedAssets) {
                if (!IsReplica(importedAsset, out GameObject gameObject))
                    continue;

                if (NetworkPrefabLookup.Instance == null) {
                    Generate();
                    return;
                }

                if (prefabs.Contains(gameObject))
                    continue;

                prefabs.Add(gameObject);
                hasChanged = true;
            }

            foreach (var deletedAsset in deletedAssets) {
                if (!IsReplica(deletedAsset, out GameObject gameObject))
                    continue;

                if (NetworkPrefabLookup.Instance == null) {
                    Generate();
                    return;
                }

                if (!prefabs.Contains(gameObject))
                    continue;

                prefabs.Remove(gameObject);
                hasChanged = true;
            }

            if (hasChanged) {
                NetworkPrefabLookup.Instance.Prefabs = prefabs.ToArray();
                EditorUtility.SetDirty(NetworkPrefabLookup.Instance);
            }
        }

        [MenuItem("Tools/Cube/Refresh NetworkPrefabLookup")]
        static void Force() {
            Generate();
            Debug.Log("Done");
        }

        static void Generate() {
            if (BuildPipeline.isBuildingPlayer)
                return; // No need to regenerate the data while building

            var prefabs = new List<GameObject>();

            var assetGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var serverAssetGuid in assetGuids) {
                var serverAssetPath = AssetDatabase.GUIDToAssetPath(serverAssetGuid);

                var isClientPrefab = serverAssetPath.IndexOf(CLIENT_PREFAB_PREFIX, StringComparison.InvariantCultureIgnoreCase) != -1;
                if (isClientPrefab)
                    continue;

                var serverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(serverAssetPath);
                if (serverPrefab == null)
                    continue;

                var isReplicaPrefab = serverPrefab.GetComponent<Replica>() != null;
                if (!isReplicaPrefab)
                    continue;

                var clientPrefab = serverPrefab;
                var clientAssetPath = serverAssetPath;
                if (serverAssetPath.IndexOf("Server_", StringComparison.InvariantCultureIgnoreCase) != -1) {
                    clientAssetPath = ReplaceString(serverAssetPath, SERVER_PREFAB_PREFIX, CLIENT_PREFAB_PREFIX, StringComparison.InvariantCultureIgnoreCase);

                    clientPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(clientAssetPath);
                    if (clientPrefab == null) {
                        Debug.LogWarning("Client Replica prefab for server prefab not found, this will lead to network errors: " + serverAssetPath);
                        continue;
                    }
                }

                var serverReplica = serverPrefab.GetComponent<Replica>();
                if (serverReplica.prefabIdx != prefabs.Count) {
                    serverReplica.prefabIdx = (ushort)prefabs.Count;
                    EditorUtility.SetDirty(serverPrefab);
                }

                var clientReplica = clientPrefab.GetComponent<Replica>();
                if (clientReplica.prefabIdx != prefabs.Count) {
                    clientReplica.prefabIdx = (ushort)prefabs.Count;
                    EditorUtility.SetDirty(clientPrefab);
                }

                // Fix copy&paste errors
                if (serverReplica.sceneIdx != 0) {
                    serverReplica.sceneIdx = 0;
                    EditorUtility.SetDirty(serverPrefab);
                }
                if (clientReplica.sceneIdx != 0) {
                    clientReplica.sceneIdx = 0;
                    EditorUtility.SetDirty(clientReplica);
                }

                prefabs.Add(clientPrefab);
            }

            var newPrefabs = prefabs.ToArray();

            var lookup = NetworkPrefabLookup.Instance;
            if (lookup.Prefabs == null || !lookup.Prefabs.SequenceEqual(newPrefabs)) {
                lookup.Prefabs = newPrefabs;
                EditorUtility.SetDirty(lookup);
            }
        }

        static string ReplaceString(string str, string oldValue, string newValue, StringComparison comparison) {
            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1) {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));
            return sb.ToString();
        }
    }
}
