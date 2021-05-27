using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using Mono.Cecil;
using System.Linq;

namespace Cube.Replication.Editor {
    [InitializeOnLoad]
    public static class CompilationPipelineHook {
        static BaseAssemblyResolver resolver;

        [MenuItem("Cube/Force Recompile")]
        static void ForceRecompile() {
            foreach (var assembly in CompilationPipeline.GetAssemblies()) {
                if (File.Exists(assembly.outputPath)) {
                    OnCompilationFinished(assembly.outputPath, new CompilerMessage[0]);
                }
            }

#if UNITY_2019_3_OR_NEWER
            EditorUtility.RequestScriptReload();
#else
            UnityEditorInternal.InternalEditorUtility.RequestScriptReload();
#endif
        }

        static CompilationPipelineHook() {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        static void OnCompilationFinished(string assemblyPath, CompilerMessage[] compilerMessages) {
            if (string.IsNullOrEmpty(assemblyPath))
                return;

            if (CompilerMessagesContainError(compilerMessages)) {
#if CUBE_DEBUG_REP
                UnityEngine.Debug.Log($"RPC Patcher stop because compile errors on target: {assemblyPath}");
#endif
                return;
            }

            if (assemblyPath.Contains("-Editor") || assemblyPath.Contains(".Editor"))
                return;

            WeaveAssembly(assemblyPath);
        }

        static bool CompilerMessagesContainError(CompilerMessage[] messages) {
            return messages.Any(msg => msg.type == CompilerMessageType.Error);
        }


        static void WeaveAssembly(string assemblyPath) {
            var dependencyPaths = GetDependecyPaths(assemblyPath);

            var asmResolver = new DefaultAssemblyResolver();
            foreach (var p in dependencyPaths) {
                asmResolver.AddSearchDirectory(p);
            }

            var name = Path.GetFileNameWithoutExtension(assemblyPath);
            var filePath = Path.Combine(Application.dataPath, "..", assemblyPath);

            EditorUtility.DisplayProgressBar("Cube RPC Patcher", $"{name} ...", 0);

            try {
                AssemblyPostProcessor.Process(filePath, asmResolver);
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        static HashSet<string> GetDependecyPaths(string assemblyPath) {
            // build directory list for later asm/symbol resolving using CompilationPipeline refs
            HashSet<string> dependencyPaths = new HashSet<string> {
                Path.GetDirectoryName(assemblyPath)
            };
            foreach (Assembly unityAsm in CompilationPipeline.GetAssemblies()) {
                if (unityAsm.outputPath == assemblyPath) {
                    foreach (string unityAsmRef in unityAsm.compiledAssemblyReferences) {
                        dependencyPaths.Add(Path.GetDirectoryName(unityAsmRef));
                    }
                }
            }

            return dependencyPaths;
        }
    }
}
