using Cube.Replication;
using UnityEditor;
using UnityEngine;

namespace Cube.Networking {
    class NetworkingEditorSettingsWindow : EditorWindow {

        [MenuItem("Tools/Cube/Editor Settings")]
        public static void ShowWindow() {
            var window = GetWindow(typeof(NetworkingEditorSettingsWindow));
            window.titleContent = new GUIContent("Cube Editor Settings");
        }

        ApplicationType _appType;

        void OnEnable() {
            var definitions = new ScriptDefinitions(BuildTargetGroup.Standalone);

            _appType |= definitions.IsSet("SERVER") ? ApplicationType.Server : ApplicationType.None;
            _appType |= definitions.IsSet("CLIENT") ? ApplicationType.Client : ApplicationType.None;
        }

        void OnGUI() {
            bool server = _appType.HasFlag(ApplicationType.Server);
            bool client = _appType.HasFlag(ApplicationType.Client);

            server = EditorGUILayout.Toggle("Server", server);
            client = EditorGUILayout.Toggle("Client", client);

            _appType = ApplicationType.None;
            _appType |= server ? ApplicationType.Server : ApplicationType.None;
            _appType |= client ? ApplicationType.Client : ApplicationType.None;

            if (GUILayout.Button("Save"))
                Save();
        }

        void Save() {
            var definitions = new ScriptDefinitions(BuildTargetGroup.Standalone);
            definitions.Set("SERVER", (_appType & ApplicationType.Server) != 0);
            definitions.Set("CLIENT", (_appType & ApplicationType.Client) != 0);
            definitions.Write();
        }

    }
}
