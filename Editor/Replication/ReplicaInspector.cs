﻿using System;
using UnityEditor;
using UnityEngine;

namespace Cube.Replication.Editor {
    [CustomEditor(typeof(Replica))]
    [CanEditMultipleObjects]
    public class ReplicaInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            if (targets.Length != 1)
                return;

            var replica = target as Replica;

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab Hash", replica.PrefabHash.ToString());
            if (replica.isSceneReplica) {
                EditorGUILayout.LabelField("Scene RID", replica.sceneIdx.ToString());
            }
            EditorGUILayout.LabelField($"{(replica.IsOwner ? "O" : "")}{(replica.isClient ? "C" : "")}{(replica.isServer ? "S" : "")}");
            GUILayout.EndHorizontal();

            if (EditorApplication.isPlaying) {
                EditorGUILayout.LabelField("RID", replica.Id.Data.ToString());

                GUILayout.BeginHorizontal();

                var what = replica.isClient ? "Server" : "Client";
                if (GUILayout.Button("Find " + what)) {
                    ApplyToCorrespondingReplica(replica, cr => EditorGUIUtility.PingObject(cr.transform.gameObject));
                }
                if (GUILayout.Button("Select " + what)) {
                    ApplyToCorrespondingReplica(replica, cr => Selection.activeGameObject = cr.transform.gameObject);
                }

                GUILayout.EndHorizontal();
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
