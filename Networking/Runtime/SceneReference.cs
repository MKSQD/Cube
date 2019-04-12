// From http://answers.unity.com/comments/1374414/view.html

using System;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Cube.Networking {
    [Serializable]
    public class SceneReference {
        [SerializeField]
        Object _sceneAsset;
        [SerializeField]
        string _sceneName = "";

        public string sceneName {
            get { return _sceneName; }
        }

        // makes it work with the existing Unity methods (LoadLevel/LoadScene)
        public static implicit operator string(SceneReference sceneRef) {
            return sceneRef.sceneName;
        }

        public static string[] ToStringArray(SceneReference[] sceneRefs) {
            var result = new string[sceneRefs.Length];
            for (int i = 0; i < sceneRefs.Length; i++)
                result[i] = sceneRefs[i].sceneName;
            return result;
        }

        public override string ToString() {
            return this;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SceneReference))]
    public class SceneReferencePropertyDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            var sceneAsset = property.FindPropertyRelative("_sceneAsset");
            var sceneName = property.FindPropertyRelative("_sceneName");
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            if (sceneAsset != null) {
                EditorGUI.BeginChangeCheck();
                var value = EditorGUI.ObjectField(position, sceneAsset.objectReferenceValue, typeof(SceneAsset), false);
                if (EditorGUI.EndChangeCheck()) {
                    sceneAsset.objectReferenceValue = value;
                    if (sceneAsset.objectReferenceValue != null) {
                        var scenePath = AssetDatabase.GetAssetPath(sceneAsset.objectReferenceValue);
                        var assetsIndex = scenePath.IndexOf("Assets", StringComparison.Ordinal) + 7;
                        var extensionIndex = scenePath.LastIndexOf(".unity", StringComparison.Ordinal);
                        scenePath = scenePath.Substring(assetsIndex, extensionIndex - assetsIndex);
                        sceneName.stringValue = scenePath;
                    }
                }
            }
            EditorGUI.EndProperty();
        }
    }
#endif
}