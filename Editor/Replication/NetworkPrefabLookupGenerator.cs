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

            // var assetGuids = AssetDatabase.FindAssets("t:Prefab");
            // foreach (var serverAssetGuid in assetGuids) {
            //     var serverAssetPath = AssetDatabase.GUIDToAssetPath(serverAssetGuid);

            //     var isClientPrefab = serverAssetPath.IndexOf(ClientPrefabPrefix, StringComparison.InvariantCultureIgnoreCase) != -1;
            //     if (isClientPrefab)
            //         continue;

            //     var serverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(serverAssetPath);
            //     if (serverPrefab == null)
            //         continue;

            //     var isReplicaPrefab = serverPrefab.GetComponent<Replica>() != null;
            //     if (!isReplicaPrefab)
            //         continue;

            //     var clientPrefab = serverPrefab;
            //     if (serverAssetPath.IndexOf("Server_", StringComparison.InvariantCultureIgnoreCase) != -1) {
            //         var clientAssetPath = serverAssetPath.ReplaceWorkaround(ServerPrefabPrefix, ClientPrefabPrefix, StringComparison.InvariantCultureIgnoreCase);

            //         clientPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(clientAssetPath);
            //         if (clientPrefab == null) {
            //             Debug.LogWarning("Client Replica prefab for server prefab not found, this will lead to network errors: " + serverAssetPath);
            //             continue;
            //         }
            //     }

            //     serverAndClientPrefabs.Add((serverAssetGuid, serverPrefab, clientPrefab));
            // }


        }

        static ushort NameToHash(string name) => (ushort)GetStableHashCode(name);

        static int GetStableHashCode(string str) {
            unchecked {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2) {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}
