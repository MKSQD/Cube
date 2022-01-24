using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace Cube.Replication {
    static class ReplicaUtils {
        public static List<Replica> GatherSceneReplicas(Scene scene) {
            Assert.IsTrue(scene.IsValid());

            var result = new List<Replica>();
            foreach (var go in scene.GetRootGameObjects()) {
                foreach (var replica in go.GetComponentsInChildren<Replica>()) {
                    if (!replica.isSceneReplica)
                        continue;

                    result.Add(replica);
                }
            }
            return result;
        }
    }
}