using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Cube.Networking {
    public class BuildWindow : EditorWindow {

        [MenuItem("Window/Cube/Build Window")]
        [MenuItem("Cube/Window/Build Window")]
        public static void ShowWindow() {
            var window = EditorWindow.GetWindow(typeof(BuildWindow));
            window.titleContent = new GUIContent("Build configuration");
        }

        List<BuildConfiguration> _configs;
        int _selectedIndex;

        void OnEnable() {
            UpdateInstallers();
        }

        void UpdateInstallers() {
            _configs = new List<BuildConfiguration>();

            var guids = AssetDatabase.FindAssets("t:BuildConfiguration");
            foreach (var guid in guids) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BuildConfiguration>(path);
                _configs.Add(asset);
            }
        }

        void OnGUI() {
            if (_configs == null || _configs.Count == 0) {
                EditorGUILayout.LabelField("No build configurations found.");
                return;
            }

            EditorGUILayout.BeginVertical();

            var data = new List<string>();
            foreach (var conf in _configs)
                data.Add(conf.name);

            EditorGUILayout.Space();
            _selectedIndex = EditorGUILayout.Popup(_selectedIndex, data.ToArray());
            EditorGUILayout.Space();

            if (_selectedIndex <= _configs.Count) {
                var config = _configs[_selectedIndex];
                var editor = UnityEditor.Editor.CreateEditor(config);
                editor.OnInspectorGUI();
            }

            EditorGUILayout.EndVertical();
        }

    }

}

