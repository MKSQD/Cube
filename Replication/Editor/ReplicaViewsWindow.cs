﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Cube.Replication {
    class ReplicaViewWindow : EditorWindow {
        ReplicaView[] _replicaViews;

        [MenuItem("Cube/Replica Views")]
        public static void ShowWindow() {
            var window = GetWindow(typeof(ReplicaViewWindow));
            window.titleContent = new GUIContent("Replica Views");
        }

        void OnGUI() {
            if (Application.isPlaying)
                DrawReplicaViews();
            else
                EditorGUILayout.LabelField(new GUIContent("Enter play mode to use"));
        }

        void Update() {
            if (!Application.isPlaying)
                return;

            SearchReplicaViews();
            Repaint();
        }

        void SearchReplicaViews() {
            _replicaViews = FindObjectsOfType<ReplicaView>();
        }

        void DrawReplicaViews() {
            if (_replicaViews == null)
                return;

            foreach (var view in _replicaViews) {
                if (view == null)
                    continue;

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(view.transform.gameObject.name);

                EditorGUILayout.Space();

                if (GUILayout.Button("find"))
                    EditorGUIUtility.PingObject(view.gameObject);

                EditorGUILayout.EndHorizontal();
            }

        }
    }
}
#endif