using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;

namespace Cube.Replication.Editor {
    [InitializeOnLoad]
    public static class OnAssemblyReload {
        static CachedAssemblyResolver resolver;

        static OnAssemblyReload() {
            CompilationPipeline.assemblyCompilationFinished += ComplicationComplete;

            var unityAssemblyPath = Path.GetDirectoryName(EditorApplication.applicationPath);
            if (unityAssemblyPath.Length == 0 || !Directory.Exists(unityAssemblyPath)) {
                Debug.LogError("Unity3d assembly path not found (" + unityAssemblyPath + ")");
                return;
            }

            var searchPathList = new List<string> {
                    unityAssemblyPath + "/Data/Managed",
                    unityAssemblyPath + "/Data/Managed/UnityEngine",
                    unityAssemblyPath + "/Data/NetStandard/ref/2.0.0",
                    unityAssemblyPath + "/Data/PlaybackEngines/windowsstandalonesupport/Managed",
                    unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/Editor",
                    unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/RuntimeEditor",
                    unityAssemblyPath + "/Data/UnityExtensions/Unity/Timeline/Runtime",
                    Application.dataPath + "/../Library/ScriptAssemblies"
                };

            resolver = new CachedAssemblyResolver();
            foreach (var path in searchPathList) {
                resolver.AddSearchDirectory(path);
            }
        }

        static void ComplicationComplete(string assemblyPath, CompilerMessage[] compilerMessages) {
            WeaveAssembly(assemblyPath);
        }

        static void WeaveAssembly(string assemblyPath) {
            if (string.IsNullOrEmpty(assemblyPath))
                return;

            string name = Path.GetFileNameWithoutExtension(assemblyPath);
            string filePath = Path.Combine(Application.dataPath, "..", assemblyPath);
            AssemblyPostProcessor.Process(filePath, resolver);
        }
    }
}
