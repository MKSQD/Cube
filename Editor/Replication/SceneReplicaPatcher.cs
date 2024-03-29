using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cube.Replication.Editor {
    [InitializeOnLoad]
    static class SceneReplicaPatcher {
        static SceneReplicaPatcher() {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        [MenuItem("Tools/Cube/Internal/Reset Scene Replica IDs")]
        static void Force() {
            if (Application.isPlaying)
                return;

            for (int i = 0; i < SceneManager.sceneCount; ++i) {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var sceneReplicas = GatherSceneReplicas(scene);
                foreach (var replica in sceneReplicas) {
                    replica.StaticId = 0;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(replica);
                }

                EditorSceneManager.MarkSceneDirty(scene);
            }
            Debug.Log("Done");
        }



        static void OnSceneSaving(Scene scene, string path) {
            var sceneReplicas = GatherSceneReplicas(scene);

            // Collect existing IDs
            var usedIdcs = new HashSet<ushort>();
            foreach (var replica in sceneReplicas) {
                if (replica.StaticId == 0)
                    continue;

                if (!usedIdcs.Contains(replica.StaticId)) {
                    usedIdcs.Add(replica.StaticId);
                } else {
                    replica.StaticId = 0;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(replica);
                }
            }

            var nextSceneIdx = 1;

            var lastUsedSceneIdx = 0;
            foreach (var replica2 in sceneReplicas) {
                lastUsedSceneIdx = Math.Max(lastUsedSceneIdx, replica2.StaticId);
            }

            // Assign new IDs
            foreach (var replica in sceneReplicas) {
                if (replica.StaticId != 0)
                    continue;

                while (usedIdcs.Contains((byte)nextSceneIdx) && nextSceneIdx < 1000) {
                    ++nextSceneIdx;
                }
                if (nextSceneIdx >= 1000) {
                    Debug.LogError("More than 1000 scene Replicas, not yet supported. Aborting.");
                    return;
                }

                replica.StaticId = (byte)nextSceneIdx;
                ++nextSceneIdx;

                PrefabUtility.RecordPrefabInstancePropertyModifications(replica);
            }
        }

        static List<Replica> GatherSceneReplicas(Scene scene) {
            var sceneReplicas = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    sceneReplicas.Add(replica);
                }
            }
            sceneReplicas.Sort((r1, r2) => r1.GetInstanceID() - r2.GetInstanceID()); // Mostly stable so scene indices don't change so often
            return sceneReplicas;
        }
    }
}