using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;

namespace Cube.Replication.Editor {
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
            searchPathList.Add(unityAssemblyPath + "/Data/Managed/UnityEngine");
            searchPathList.Add(unityAssemblyPath + "/Data/NetStandard/ref/2.0.0");
            searchPathList.Add(unityAssemblyPath + "/Data/PlaybackEngines/windowsstandalonesupport/Managed");
            searchPathList.Add(unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/Editor");
            searchPathList.Add(unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/RuntimeEditor");
            searchPathList.Add(unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/Runtime");
            searchPathList.Add(Application.dataPath + "/../Library/ScriptAssemblies");

            AssemblyPostProcessor.Start(assemblyPath, searchPathList.ToArray(), true);
        }
    }
}
