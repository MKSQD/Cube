using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

#if UNITY_EDITOR
namespace Cube.Replication.Editor {
    public static class AssemblyPostProcessor {
        [Flags]
        public enum PatcherOptions {
            None = 0,
            SkipSymbols = 1
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="System.Exception">on fail</exception>
        /// <param name="appType"></param>
        /// <param name="assemblyPath"></param>
        /// <param name="assemblySearchPaths"></param>
        /// <param name="withSymbols"></param>
        public static void Start(string assemblyPath, IAssemblyResolver assemblyResolver, PatcherOptions options = PatcherOptions.None) {
            if (!File.Exists(assemblyPath)) {
                Debug.Log("RPC Patcher skipped file (not found): " + assemblyPath);
                return;
            }

            var appType = ApplicationType.Client | ApplicationType.Server;

            using (var assemblyLocker = new ReloadAssembiesLocker()) {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                try {
                    var readerParameters = new ReaderParameters() {
                        AssemblyResolver = assemblyResolver,
                        ReadWrite = true
                    };

                    var writerParameters = new WriterParameters();

                    if (!options.HasFlag(PatcherOptions.SkipSymbols)) {
                        // mdbs have the naming convention myDll.dll.mdb whereas pdbs have myDll.pdb
                        var mdbPath = assemblyPath + ".mdb";
                        var pdbPath = assemblyPath.Substring(0, assemblyPath.Length - 3) + "pdb";

                        // Figure out if there's an pdb/mdb to go with it
                        if (File.Exists(pdbPath)) {
                            readerParameters.ReadSymbols = true;
                            readerParameters.SymbolReaderProvider = new Mono.Cecil.Pdb.PdbReaderProvider();
                            writerParameters.WriteSymbols = true;
                            writerParameters.SymbolWriterProvider = new Mono.Cecil.Mdb.MdbWriterProvider(); // pdb written out as mdb, as mono can't work with pdbs
                        } else if (File.Exists(mdbPath)) {
                            readerParameters.ReadSymbols = true;
                            readerParameters.SymbolReaderProvider = new Mono.Cecil.Mdb.MdbReaderProvider();
                            writerParameters.WriteSymbols = true;
                            writerParameters.SymbolWriterProvider = new Mono.Cecil.Mdb.MdbWriterProvider();
                        } else {
                            readerParameters.ReadSymbols = false;
                            readerParameters.SymbolReaderProvider = null;
                            writerParameters.WriteSymbols = false;
                            writerParameters.SymbolWriterProvider = null;
                        }
                    }

                    using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters)) {
                        if (assembly.FullName == Assembly.GetAssembly(typeof(AssemblyPostProcessor)).FullName)
                            return; // Skip Cube.Replication.Editor

                        var hasRefToReplication = false;
                        foreach (var reference in assembly.MainModule.AssemblyReferences) {
                            if (reference.Name == "Cube.Replication") {
                                hasRefToReplication = true;
                                break;
                            }
                        }
                        if (!hasRefToReplication)
                            return; // Skip assemblies not referencing Cube.Replication

                        var rpcProcessor = new RpcPostProcessor(appType, assembly.MainModule);
                        foreach (var module in assembly.Modules) {
                            rpcProcessor.Process(module);
                        }

                        assembly.Write(writerParameters);
                    }
                } catch (InvalidOperationException) {
                    // InvalidOperationException: Operation is not valid due to the current state of the object.
                    // Assembly is in use
                } catch (Exception e) {
                    throw new Exception("RPC Patcher failed (assembly = " + assemblyPath + ", appType = " + appType.ToString() + ")", e);
                }

                watch.Stop();
#if CUBE_DEBUG_REP
                Debug.Log("RPC Patcher finished after " + watch.ElapsedMilliseconds + "ms (assembly = " + assemblyPath + ", appType = " + appType.ToString() + ")");
#endif
            }
        }
    }

    public class CachedAssemblyResolver : BaseAssemblyResolver {
        Dictionary<string, AssemblyDefinition> _cache = new Dictionary<string, AssemblyDefinition>();

        public override AssemblyDefinition Resolve(AssemblyNameReference name) {
            AssemblyDefinition assembly;
            if (_cache.TryGetValue(name.FullName, out assembly))
                return assembly;

            assembly = base.Resolve(name);
            _cache[name.FullName] = assembly;

            return assembly;
        }

        protected override void Dispose(bool disposing) {
            if (!disposing)
                return;

            foreach (var assembly in _cache) {
                assembly.Value.Dispose();
            }
            _cache.Clear();
        }
    }

    class ReloadAssembiesLocker : IDisposable {
        public ReloadAssembiesLocker() {
            EditorApplication.LockReloadAssemblies();
        }

        public void Dispose() {
            EditorApplication.UnlockReloadAssemblies();
        }
    }
}

#endif
