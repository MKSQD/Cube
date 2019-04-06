using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;

namespace Cube.Replication {
    [InitializeOnLoad]
    public static class OnAssemblyReload {
        static OnAssemblyReload() {
            CompilationPipeline.assemblyCompilationFinished -= OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages) {
            if (BuildPipeline.isBuildingPlayer)
                return;

            string unityAssemblyPath = Path.GetDirectoryName(EditorApplication.applicationPath);

            if (unityAssemblyPath.Length == 0 || !Directory.Exists(unityAssemblyPath)) {
                Debug.LogError("Unity3d assembly path not found (" + unityAssemblyPath + ")");
                return;
            }

            var searchPathList = new List<string>();
            searchPathList.Add(unityAssemblyPath + "/Data/Managed");
            searchPathList.Add(unityAssemblyPath + "/Data/PlaybackEngines/windowsstandalonesupport/Managed");
            searchPathList.Add(unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/Editor");
            searchPathList.Add(unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/RuntimeEditor");
            searchPathList.Add(unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/Runtime");
            searchPathList.Add(Application.dataPath + "/../Library/ScriptAssemblies");

            try {
                var definitions = new ScriptDefinitions(BuildTargetGroup.Standalone);
                
                var appType = ApplicationType.None;
                appType |= definitions.IsSet("CLIENT") ? ApplicationType.Client : 0;
                appType |= definitions.IsSet("SERVER") ? ApplicationType.Server : 0;
                
                AssemblyPostProcessor.Start(appType, assemblyPath, searchPathList.ToArray(), true);
            }
            catch (Exception e) {
                Debug.LogError(e);
            }
        }
    }
}
