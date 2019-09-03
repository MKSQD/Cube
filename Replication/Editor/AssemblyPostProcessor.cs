using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace Cube.Replication.Editor {
    class ReloadAssembiesLocker : IDisposable {
        public ReloadAssembiesLocker() {
            EditorApplication.LockReloadAssemblies();
        }

        public void Dispose() {
            EditorApplication.UnlockReloadAssemblies();
        }
    }

    public static class AssemblyPostProcessor {
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="System.Exception">on fail</exception>
        /// <param name="appType"></param>
        /// <param name="assemblyPath"></param>
        /// <param name="assemblySearchPaths"></param>
        /// <param name="withSymbols"></param>
        public static void Start(string assemblyPath, string[] assemblySearchPaths, bool withSymbols) {
            var appType = ApplicationType.Client | ApplicationType.Server;

            using (var locker = new ReloadAssembiesLocker()) {
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();

                if (!File.Exists(assemblyPath))
                    throw new Exception("RPC Patcher skipped - File not exists:" + assemblyPath);

                using (var resolver = new CachedAssemblyResolver()) {
                    foreach (var path in assemblySearchPaths) {
                        resolver.AddSearchDirectory(path);
                    }

                    try {
                        var readerParameters = new ReaderParameters() {
                            AssemblyResolver = resolver,
                            ReadSymbols = withSymbols,
                            ReadWrite = true
                        };

                        using (var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters)) {
                            //skip Cube.Replication.Editor
                            if (assembly.FullName == Assembly.GetAssembly(typeof(AssemblyPostProcessor)).FullName)
                                return;
                            
                            bool hasRefToReplication = false;
                            foreach (var reference in assembly.MainModule.AssemblyReferences) {
                                if (reference.Name == "Cube.Replication") {
                                    hasRefToReplication = true;
                                    break;
                                }
                            }

                            if (!hasRefToReplication)
                                return;

                            foreach (var module in assembly.Modules) {
                                var rpcProcessor = new RpcPostProcessor(appType, module);
                                rpcProcessor.Process();

                                var varProcessor = new VarPostProcessor(appType, module);
                                varProcessor.Process();
                            }

                            assembly.Write(new WriterParameters() {
                                WriteSymbols = withSymbols
                            });
                        }
                    }
                    catch (Exception e) {
                        throw new Exception("RPC Patcher failed (assembly path = " + assemblyPath + ", appType = " + appType.ToString() + ")", e);
                    }
                }

                watch.Stop();
#if CUBE_DEBUG_REP
                Debug.Log("RPC Patcher finished after " + watch.Elapsed + " (assemblyPaths = " + assemblyPath + ", appType = " + appType.ToString() + ")");
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
            if (disposing) {
                foreach (var assembly in _cache) {
                    assembly.Value.Dispose();
                }
                _cache.Clear();
            }
        }
    }
}

#endif
