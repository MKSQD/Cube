using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public class SceneReplicaUtil {
        public static List<Replica> FindSceneReplicasInScene(Scene scene) {
            var result = new List<Replica>();

            var rootObjects = scene.GetRootGameObjects();
            if (rootObjects.Length == 0)
                return result;
            
            Action<GameObject> findReplicas = null;
            findReplicas = (obj) => {
                var replica = obj.GetComponent<Replica>();
                if (replica != null && replica.gameObject.activeInHierarchy == true) {
                    result.Add(replica);
                    return;
                }

                foreach (Transform child in obj.transform) {
                    findReplicas(child.gameObject);
                }
            };

            foreach (var obj in rootObjects) {
                findReplicas(obj);
            }

            return result;
        }

        public static SceneReplicaWrapper GetSceneReplicaWrapper(Scene scene) {
            var rootObjects = scene.GetRootGameObjects();
            if (rootObjects.Length == 0)
                return null;

            foreach (var obj in rootObjects) {
                var wrapper = obj.GetComponent<SceneReplicaWrapper>();
                if (wrapper != null)
                    return wrapper;
            }

            return null;
        }
    }
}
