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
        [MenuItem("Cube/Generated/Generate NetworkPrefabLookup")]
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
                serverReplica.sceneIdx = SceneReplicaUtil.INVALID_SCENE_IDX;

                var clientReplica = clientPrefab.GetComponent<Replica>();
                clientReplica.prefabIdx = nextPrefabIdx;
                clientReplica.sceneIdx = SceneReplicaUtil.INVALID_SCENE_IDX;

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
            var rootObjects = new List<GameObject>(scene.GetRootGameObjects());
            if (rootObjects.Count == 0)
                return;

            //
            for (int i = 0; i < rootObjects.Count; i++) {
                if (rootObjects[i].GetComponent<SceneReplicaWrapper>() != null) {
                    GameObject.DestroyImmediate(rootObjects[i]);
                    rootObjects.RemoveAt(i);
                    break;
                }
            }

            var replicas = SceneReplicaUtil.FindSceneReplicasInScene(scene);
            if (replicas.Count == 0)
                return;

            //
            var tmp = new GameObject();
            var wrapperObj = GameObject.Instantiate(tmp, Vector3.zero, Quaternion.identity, rootObjects[0].transform);
            GameObject.DestroyImmediate(tmp);
            wrapperObj.name = "__SCENE_REPLICA_WRAPPER__";
            wrapperObj.transform.parent = null;
            //wrapperObj.hideFlags = HideFlags.HideInHierarchy;

            var sceneReplicaWrapper = wrapperObj.AddComponent<SceneReplicaWrapper>();

            byte sceneId = GetSceneId(scene);
            sceneReplicaWrapper.sceneId = sceneId;

            //
            ushort prefabIdx = 0;
            foreach (var replica in replicas) {
                EditorUtility.DisplayProgressBar("Process SceneReplicas", prefabIdx + " / " + replicas.Count, (float)prefabIdx / (float)replicas.Count);

                var prefabReplica = PrefabUtility.GetCorrespondingObjectFromSource(replica) as Replica;
                if (prefabReplica == null) {
                    replica.sceneIdx = sceneId;
                    replica.prefabIdx = prefabIdx;
                }
                else {
                    if (prefabReplica.name.StartsWith(CLIENT_PREFAB_PREFIX, StringComparison.InvariantCultureIgnoreCase)) {
                        Debug.LogWarning("Cannot use ClientPrefabs as SceneReplica (" + prefabReplica.name + ")");
                    }

                    //reset idx in prefab instance (required when prefab has been created from scene object)
                    replica.sceneIdx = SceneReplicaUtil.INVALID_SCENE_IDX;
                    replica.prefabIdx = prefabReplica.prefabIdx;
                    continue;
                }

                var clientBlueprint = GameObject.Instantiate(replica, sceneReplicaWrapper.transform).gameObject;
                clientBlueprint.SetActive(false);
                prefabIdx++;
            }

            EditorUtility.ClearProgressBar();
        }

        static ScenesInfo GetOrCreateScenesInfoFile() {
            var scenesInfoFilePath = "Assets/Cube/Resources/SceneInfos.asset";

            var sceneInfos = AssetDatabase.LoadAssetAtPath<ScenesInfo>(scenesInfoFilePath);
            if (sceneInfos == null) {
                sceneInfos = ScriptableObject.CreateInstance<ScenesInfo>();
                sceneInfos.infos = new List<ScenesInfo.Entry>();

                AssetDatabase.CreateAsset(sceneInfos, scenesInfoFilePath);
            }

            return sceneInfos;
        }

        static byte GetSceneId(Scene scene) {
            var sceneInfos = GetOrCreateScenesInfoFile();

            byte sceneId = byte.MaxValue;
            foreach (var entry in sceneInfos.infos) {
                if (entry.scenePath == scene.path) {
                    sceneId = entry.id;
                    break;
                }
            }

            if (sceneId == byte.MaxValue) {
                //#TODO check max scene id -> error
                sceneId = (byte)sceneInfos.infos.Count;
                sceneInfos.infos.Add(new ScenesInfo.Entry {
                    id = sceneId,
                    scenePath = scene.path
                });
            }

            return sceneId;
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
