using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Cube.Replication.Editor {
    public static class NetworkPrefabLookupGenerator {
        const string ClientPrefabPrefix = "Client_";
        const string ServerPrefabPrefix = "Server_";

        [MenuItem("Tools/Cube/Update Replica Prefabs")]
        static void Force() {
            Generate();
        }

        static void Generate() {
            var serverAndClientPrefabs = new List<(string, GameObject, GameObject)>();

            var assetGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var serverAssetGuid in assetGuids) {
                var serverAssetPath = AssetDatabase.GUIDToAssetPath(serverAssetGuid);

                var isClientPrefab = serverAssetPath.IndexOf(ClientPrefabPrefix, StringComparison.InvariantCultureIgnoreCase) != -1;
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
                    var clientAssetPath = ReplaceString(serverAssetPath, ServerPrefabPrefix, ClientPrefabPrefix, StringComparison.InvariantCultureIgnoreCase);

                    clientPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(clientAssetPath);
                    if (clientPrefab == null) {
                        Debug.LogWarning("Client Replica prefab for server prefab not found, this will lead to network errors: " + serverAssetPath);
                        continue;
                    }
                }

                serverAndClientPrefabs.Add((serverAssetGuid, serverPrefab, clientPrefab));
            }

            int numChanged = 0;

            var prefabs = new List<GameObject>(serverAndClientPrefabs.Count);
            ushort nextId = 1;
            foreach (var tuple in serverAndClientPrefabs.OrderBy(tuple => tuple.Item1)) {
                var serverPrefab = tuple.Item2;
                var clientPrefab = tuple.Item3;
                var id = nextId++;

                var serverReplica = serverPrefab.GetComponent<Replica>();
                if (serverReplica.prefabIdx != id) {
                    serverReplica.prefabIdx = id;
                    EditorUtility.SetDirty(serverPrefab);
                    ++numChanged;
                }

                var clientReplica = clientPrefab.GetComponent<Replica>();
                if (clientReplica.prefabIdx != id) {
                    clientReplica.prefabIdx = id;
                    EditorUtility.SetDirty(clientPrefab);
                    ++numChanged;
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

            Debug.Log($"done (#changed={numChanged})");
        }

        static string ReplaceString(string str, string oldValue, string newValue, StringComparison comparison) {
            var sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1) {
                sb.Append(str[previousIndex..index]);
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str[previousIndex..]);
            return sb.ToString();
        }
    }
}
