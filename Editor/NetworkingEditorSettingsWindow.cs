using UnityEngine;
using UnityEditor;
using Cube.Replication;

namespace Cube.Networking {
    class NetworkingEditorSettingsWindow : EditorWindow {

        [MenuItem("Cube/Editor Settings")]
        public static void ShowWindow() {
            var window = GetWindow(typeof(NetworkingEditorSettingsWindow));
            window.titleContent = new GUIContent("Cube Editor Settings");
        }

        ApplicationType _appType;
        bool _debugReplication = false;
        bool _debugTransport = false;

        void OnEnable() {
            var definitions = new ScriptDefinitions(BuildTargetGroup.Standalone);

            _appType |= definitions.IsSet("SERVER") ? ApplicationType.Server : ApplicationType.None;
            _appType |= definitions.IsSet("CLIENT") ? ApplicationType.Client : ApplicationType.None;
            _debugReplication = definitions.IsSet("CUBE_DEBUG_REP");
            _debugTransport = definitions.IsSet("CUBE_DEBUG_TRA");
        }

        void OnGUI() {
            bool server = _appType.HasFlag(ApplicationType.Server);
            bool client = _appType.HasFlag(ApplicationType.Client);

            bool firstStart = !server && !client && !_debugReplication;

            GUI.enabled = client || firstStart;
            server = EditorGUILayout.Toggle("Server", server);

            GUI.enabled = server || firstStart;
            client = EditorGUILayout.Toggle("Client", client);
            GUI.enabled = true;

            _debugReplication = EditorGUILayout.Toggle("Debug Replication", _debugReplication);
            _debugTransport = EditorGUILayout.Toggle("Debug Transport", _debugTransport);

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
            definitions.Set("CUBE_DEBUG_REP", _debugReplication);
            definitions.Set("CUBE_DEBUG_TRA", _debugTransport);
            definitions.Write();
        }

    }
}
