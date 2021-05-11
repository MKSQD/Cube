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

            EditorGUILayout.LabelField("Is Owner", replica.IsOwner.ToString());

            EditorGUILayout.LabelField("Prefab ID", replica.prefabIdx.ToString());

            if (EditorApplication.isPlaying) {
                EditorGUILayout.LabelField("Replica ID", replica.Id.Data.ToString());
            }

            if (replica.isSceneReplica) {
                EditorGUILayout.LabelField("Scene Idx", replica.sceneIdx.ToString());
            }

            if (EditorApplication.isPlaying) {
                if (GUILayout.Button("Find " + (replica.isClient ? "Server" : "Client") + " Replica")) {
                    ApplyToCorrespondingReplica(replica, cr => EditorGUIUtility.PingObject(cr.transform.gameObject));
                }
                if (GUILayout.Button("Select " + (replica.isClient ? "Server" : "Client") + " Replica")) {
                    ApplyToCorrespondingReplica(replica, cr => Selection.activeGameObject = cr.transform.gameObject);
                }
            }
        }

        void ApplyToCorrespondingReplica(Replica replica, Action<Replica> func) {
            Action<GameObject> impl = (go) => {
                var correspondingReplicas = go.GetComponentsInChildren<Replica>();
                foreach (var correspondingReplica in correspondingReplicas) {
                    if (correspondingReplica.Id == replica.Id) {
                        func(correspondingReplica);
                        break;
                    }
                }
            };

            if (replica.isClient) {
                var otherReplica = ServerReplicaManager.Main.GetReplica(replica.Id);
                if (otherReplica != null) {
                    impl(otherReplica.gameObject);
                }
            }

            if (replica.isServer) {
                foreach (var replicaManager in ClientReplicaManager.All) {
                    var otherReplica = replicaManager.GetReplica(replica.Id);
                    if (otherReplica == null)
                        continue;

                    impl(otherReplica.gameObject);
                    break;
                }
            }
        }
    }
}
#endif
