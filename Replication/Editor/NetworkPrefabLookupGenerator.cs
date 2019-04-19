using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public class NetworkPrefabLookupGenerator {
        const string CLIENT_PREFAB_PREFIX = "Client_";
        const string SERVER_PREFAB_PREFIX = "Server_";

        [DidReloadScripts]
        public static void GenerateNetworkPrefabLookup() {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;

            var prefabs = new List<GameObject>();

            ushort nextPrefabIdx = 0;
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
                if (serverAssetPath.IndexOf("Server_", StringComparison.InvariantCultureIgnoreCase) != -1) {
                    var clientAssetPath = ReplaceString(serverAssetPath, SERVER_PREFAB_PREFIX, CLIENT_PREFAB_PREFIX, StringComparison.InvariantCultureIgnoreCase);

                    clientPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(clientAssetPath);
                    if (clientPrefab == null) {
                        Debug.LogWarning("Client Replica prefab for server prefab not found, this will lead to network errors: " + serverAssetPath);
                        continue;
                    }
                }

                var serverReplica = serverPrefab.GetComponent<Replica>();
                serverReplica.prefabIdx = nextPrefabIdx;

                var clientReplica = clientPrefab.GetComponent<Replica>();
                clientReplica.prefabIdx = nextPrefabIdx;

                prefabs.Add(clientPrefab);
                nextPrefabIdx++;
            }

            //GetOrCreateLookup()
            var path = "Assets/Cube/Resources/NetworkPrefabLookup.asset";

            var lookup = AssetDatabase.LoadAssetAtPath<NetworkPrefabLookup>(path);
            if (lookup == null) {
                lookup = ScriptableObject.CreateInstance<NetworkPrefabLookup>();

                AssetDatabase.CreateAsset(lookup, path);
            }
            ////////////////

            lookup.prefabs = prefabs.ToArray();
            EditorUtility.SetDirty(lookup);
            AssetDatabase.SaveAssets();
        }

        static void OnSceneSaving(Scene scene, string path) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                var replica = go.GetComponent<Replica>();
                if (replica == null)
                    continue;

                sceneReplicas.Add(replica);
            }

            sceneReplicas.Sort((r1, r2) => r1.GetInstanceID() - r2.GetInstanceID()); // Mostly stable so sceneIdxs don't change so often

            var usedIdxs = new HashSet<byte>();
            foreach (var replica in sceneReplicas) {
                if (replica.sceneIdx == 0)
                    continue;

                if (!usedIdxs.Contains(replica.sceneIdx)) {
                    usedIdxs.Add(replica.sceneIdx);
                }
                else {
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
