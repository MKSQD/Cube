using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

#if UNITY_EDITOR
namespace Cube.Replication.Editor {
    public static class AssemblyPostProcessor {
        public static void Process(string assemblyPath, IAssemblyResolver assemblyResolver) {
            if (!File.Exists(assemblyPath)) {
#if CUBE_DEBUG_REP
                Debug.Log("RPC Patcher skipped file (not found): " + assemblyPath);
#endif
                return;
            }

            if (assemblyPath.EndsWith("Cube.Replication.Editor.dll", StringComparison.InvariantCultureIgnoreCase)) {
#if CUBE_DEBUG_REP
                Debug.Log("RPC Patcher skipped file (is Replication.Editor): " + assemblyPath);
#endif
                return;
            }

            if (assemblyPath.EndsWith("Cube.Replication.Editor.Patcher.dll", StringComparison.InvariantCultureIgnoreCase)) {
#if CUBE_DEBUG_REP
                Debug.Log("RPC Patcher skipped file (is Replication.Editor.Patcher): " + assemblyPath);
#endif
                return;
            }

            var appType = ApplicationType.Client | ApplicationType.Server;

            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            try {
                var readerParameters = new ReaderParameters() {
                    AssemblyResolver = assemblyResolver,
                    ReadWrite = true,
                    ReadingMode = ReadingMode.Immediate,
                    ReadSymbols = true
                };

                using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters)) {
                    var hasRefToReplication = assembly.MainModule.AssemblyReferences.Any(r => r.Name == "Cube.Replication");
                    if (!hasRefToReplication)
                        return; // Skip assemblies not referencing Cube.Replication

                    bool anythingChanged = false;

                    var rpcProcessor = new RpcPostProcessor(appType, assembly.MainModule);
                    foreach (var module in assembly.Modules) {
                        var changed = rpcProcessor.Process(module);
                        if (changed) {
                            anythingChanged = true;
                        }
                    }

                    if (anythingChanged) {
                        var writerParameters = new WriterParameters() {
                            WriteSymbols = true
                        };

                        assembly.Write(writerParameters);
                    }
                }
#pragma warning disable CS016 // The variable 'e' is declared but never used
            } catch (IOException e) {
#if CUBE_DEBUG_REP
                Debug.Log($"While processing {assemblyPath}:");
                Debug.LogWarning(e);
#endif
            } catch (InvalidOperationException e) {
#if CUBE_DEBUG_REP
                Debug.LogWarning(e);
#endif
            } catch (Exception e) {
                throw new Exception($"RPC Patcher failed (assembly = {assemblyPath}, appType = {appType})", e);
            }
#pragma warning restore CS016

            watch.Stop();
#if CUBE_DEBUG_REP
            Debug.Log("RPC Patcher finished after " + watch.ElapsedMilliseconds + "ms (assembly = " + assemblyPath + ", appType = " + appType.ToString() + ")");
#endif
        }
    }
}

#endif
