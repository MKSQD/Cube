using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cube.Networking.Replicas {
    public class SceneReplicaUtil {
        public const byte INVALID_SCENE_IDX = byte.MaxValue;

        public static List<Replica> FindSceneReplicasInScene(Scene scene) {
            var rootObjects = scene.GetRootGameObjects();
            if (rootObjects.Length == 0)
                return null;

            var replicas = new List<Replica>();
            Action<GameObject> findReplicas = null;
            findReplicas = (obj) => {
                var replica = obj.GetComponent<Replica>();
                if (replica != null && replica.gameObject.activeInHierarchy == true) {
                    replicas.Add(replica);
                    return;
                }

                foreach (Transform child in obj.transform)
                    findReplicas(child.gameObject);
            };

            foreach (var obj in rootObjects)
                findReplicas(obj);

            return replicas;
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
