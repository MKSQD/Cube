using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Cube.Replication {
    [CustomEditor(typeof(NetworkPrefabs))]
    public class NetworkPrefabsEditor : UnityEditor.Editor {
        const string ClientPrefabPrefix = "Client_";
        const string ServerPrefabPrefix = "Server_";

        Vector2 _scrollPos;

        public override void OnInspectorGUI() {


            var prefabs = (NetworkPrefabs)target;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            for (int i = 0; i < prefabs.Pairs.Length; ++i) {
                GUILayout.BeginHorizontal();
                var pair = prefabs.Pairs[i];
                GUILayout.BeginVertical();
                GUILayout.Label($"{i}:");
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                GUILayout.Label(pair.Server?.name ?? "-");
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                GUILayout.Label(pair.Client?.name ?? "-");
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Find prefabs")) {
                var pairs = prefabs.Pairs.ToList();

                var assetGuids = AssetDatabase.FindAssets("t:Prefab");
                foreach (var serverAssetGuid in assetGuids) {
                    var serverAssetPath = AssetDatabase.GUIDToAssetPath(serverAssetGuid);

                    var isClientPrefab = serverAssetPath.IndexOf(ClientPrefabPrefix, StringComparison.InvariantCultureIgnoreCase) != -1;
                    if (isClientPrefab)
                        continue;

                    var serverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(serverAssetPath);
                    if (serverPrefab == null)
                        continue;

                    if (pairs.Any(pair => pair.Server == serverPrefab))
                        continue;

                    var isReplicaPrefab = serverPrefab.GetComponent<Replica>() != null;
                    if (!isReplicaPrefab)
                        continue;

                    var clientPrefab = serverPrefab;
                    if (serverAssetPath.IndexOf("Server_", StringComparison.InvariantCultureIgnoreCase) != -1) {
                        var clientAssetPath = ReplaceWorkaround(serverAssetPath, ServerPrefabPrefix, ClientPrefabPrefix, StringComparison.InvariantCultureIgnoreCase);

                        clientPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(clientAssetPath);
                        if (clientPrefab == null) {
                            Debug.LogWarning("Client Replica prefab for server prefab not found, this will lead to network errors: " + serverAssetPath);
                            continue;
                        }
                    }

                    pairs.Add(new NetworkPrefabs.PrefabPair() { Server = serverPrefab, Client = clientPrefab });
                }

                prefabs.Pairs = pairs.ToArray();
                EditorUtility.SetDirty(prefabs);
            }

            DrawDefaultInspector();
        }

        static string ReplaceWorkaround(string str, string oldValue, string newValue, StringComparison comparison) {
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