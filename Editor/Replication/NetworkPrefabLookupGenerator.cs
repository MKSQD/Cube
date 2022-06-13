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
        public static void Force() {
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
            var hashes = new List<ushort>(serverAndClientPrefabs.Count);
            foreach (var tuple in serverAndClientPrefabs.OrderBy(tuple => tuple.Item1)) {
                var serverPrefab = tuple.Item2;
                var clientPrefab = tuple.Item3;

                var hash = NameToHash(serverPrefab.name);
                if (hashes.Contains(hash)) {
                    Debug.LogWarning($"Duplicated Replica Prefab Hash {serverPrefab.name} ({hash})");
                    continue;
                }

                hashes.Add(hash);

                var serverReplica = serverPrefab.GetComponent<Replica>();
                if (serverReplica.PrefabHash != hash) {
                    serverReplica.PrefabHash = hash;
                    EditorUtility.SetDirty(serverPrefab);
                    ++numChanged;
                }

                var clientReplica = clientPrefab.GetComponent<Replica>();
                if (clientReplica.PrefabHash != hash) {
                    clientReplica.PrefabHash = hash;
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

            var hashesArray = hashes.ToArray();
            var prefabsArray = prefabs.ToArray();
            Array.Sort(hashesArray, prefabsArray);

            var dirty = false;

            var lookup = NetworkPrefabLookup.Instance;
            if (lookup.Prefabs == null || !lookup.Prefabs.SequenceEqual(prefabsArray)) {
                lookup.Prefabs = prefabsArray;
                dirty |= true;

            }
            if (lookup.Hashes == null || !lookup.Hashes.SequenceEqual(hashesArray)) {
                lookup.Hashes = hashesArray;
                dirty |= true;
            }

            if (dirty) {
                EditorUtility.SetDirty(lookup);
            }

            Debug.Log($"done (#changed={numChanged})");
        }

        static ushort NameToHash(string name) => (ushort)name.GetHashCode();

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
