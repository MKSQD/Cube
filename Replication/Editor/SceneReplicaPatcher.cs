using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Cube.Replication.Editor {
    [InitializeOnLoad]
    static class SceneReplicaPatcher {
        static SceneReplicaPatcher() {
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        static void OnSceneSaving(Scene scene, string path) {
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

            var lastUsedSceneIdx = 0;
            foreach (var replica2 in sceneReplicas) {
                lastUsedSceneIdx = Math.Max(lastUsedSceneIdx, replica2.sceneIdx);
            }

            foreach (var replica in sceneReplicas) {
                if (replica.sceneIdx != 0)
                    continue;

                ++lastUsedSceneIdx;
                replica.sceneIdx = (byte)lastUsedSceneIdx;

                PrefabUtility.RecordPrefabInstancePropertyModifications(replica);
            }
        }
    }
}