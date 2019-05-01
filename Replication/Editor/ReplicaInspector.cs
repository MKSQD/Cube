#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System;

namespace Cube.Replication.Editor {
    [CustomEditor(typeof(Replica))]
    [CanEditMultipleObjects]
    public class ReplicaInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            if (targets.Length != 1)
                return;

            var replica = target as Replica;

            EditorGUILayout.LabelField("Scene Idx", replica.sceneIdx.ToString());

            if (PrefabUtility.GetPrefabAssetType(replica) == PrefabAssetType.NotAPrefab) {
                EditorGUILayout.LabelField("Prefab Id", replica.prefabIdx.ToString());
            }

            if (EditorApplication.isPlaying) {
                EditorGUILayout.LabelField("Replica Id", replica.id.data.ToString());
               
                if (GUILayout.Button("Find corresponding Replica")) {
                    PingCorrespondingReplica();
                }
            }
        }

        void PingCorrespondingReplica() {
            var replica = target as Replica;
            if (replica == null)
                return;

            Action<GameObject> pingMatching = (go) => {
                var correspondingReplicas = go.GetComponentsInChildren<Replica>();
                foreach (var correspondingReplica in correspondingReplicas) {
                    if (correspondingReplica.id == replica.id) {
                        EditorGUIUtility.PingObject(correspondingReplica.transform.gameObject);
                        break;
                    }
                }
            };

            throw new Exception("Fixme");
//             if (replica.isClient) {
//                 foreach (var server in UnityServer.all) {
//                     pingMatching(server.gameObject);
//                 }
//             }
// 
//             if (replica.isServer) {
//                 foreach (var client in UnityClient.all) {
//                     pingMatching(client.gameObject);
//                 }
//             }
        }
    }
}
#endif
