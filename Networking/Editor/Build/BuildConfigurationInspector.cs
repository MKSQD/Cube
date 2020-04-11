using UnityEngine;
using UnityEditor;

namespace Cube.Networking.Editor {
    [CustomEditor(typeof(BuildConfiguration), true)]
    public class BuildConfigurationInspector : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            var buildConfiguration = (BuildConfiguration)target;

            var editor = CreateEditor(buildConfiguration);
            editor.DrawDefaultInspector();

            if (GUILayout.Button("Build")) {
                Build.BuildWithConfiguration((BuildConfiguration)target);
                Debug.Log("Build successful");
            }
        }
    }
}
