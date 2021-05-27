using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public class NetworkPrefabLookupGenerator : AssetPostprocessor {
        const string CLIENT_PREFAB_PREFIX = "Client_";
        const string SERVER_PREFAB_PREFIX = "Server_";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
            EditorSceneManager.sceneSaving -= ProcessSceneReplicasInScene;
            EditorSceneManager.sceneSaving += ProcessSceneReplicasInScene;

            Generate();
        }

        [MenuItem("Cube/Internal/Force refresh NetworkPrefabLookup")]
        static void Force() {
            Generate();
            Debug.Log("Done");
        }

        static void Generate() {
            if (BuildPipeline.isBuildingPlayer)
                return; // No need to regenerate the data while building

            var prefabs = new List<GameObject>();

            var assetGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var assetGuid in assetGuids) {
                var serverAssetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

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
                var clientAssetPath = "";
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
            if (lookup.Prefabs == null || !lookup.Prefabs.Equals(newPrefabs)) {
                lookup.Prefabs = newPrefabs;
                EditorUtility.SetDirty(lookup);
            }
        }

        static void ProcessSceneReplicasInScene(Scene scene, string path) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    sceneReplicas.Add(replica);
                }
            }

            sceneReplicas.Sort((r1, r2) => r1.GetInstanceID() - r2.GetInstanceID()); // Mostly stable so scene indices don't change so often

            var usedIdxs = new HashSet<byte>();
            foreach (var replica in sceneReplicas) {
                if (replica.sceneIdx == 0)
                    continue;

                if (!usedIdxs.Contains(replica.sceneIdx)) {
                    usedIdxs.Add(replica.sceneIdx);
                } else {
                    replica.sceneIdx = 0;
                }
            }

            foreach (var replica in sceneReplicas) {
                if (replica.sceneIdx != 0)
                    continue;

                var lastUsedSceneIdx = 0;
                foreach (var replica2 in sceneReplicas) {
                    lastUsedSceneIdx = Math.Max(lastUsedSceneIdx, replica2.sceneIdx);
                }

                replica.sceneIdx = (byte)(lastUsedSceneIdx + 1);

                PrefabUtility.RecordPrefabInstancePropertyModifications(replica);
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
