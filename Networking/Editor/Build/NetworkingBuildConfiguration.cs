using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using UnityEditor;
using Cube.Replication.Editor;
using UnityEditor.AddressableAssets.Settings;

namespace Cube.Networking.Editor {
    /// <summary>
    /// Base class for build configurations
    /// </summary>
    [CreateAssetMenu(menuName = "Cube/BuildSystem/NetworkingBuildConfiguration")]
    public class NetworkingBuildConfiguration : BuildConfiguration {
        override public void OnPreProcessBuild() {
            AddressableAssetSettings.BuildPlayerContent();
        }

        override public void OnPostProcessBuild(BuildReport report) {
            if (report.summary.result != BuildResult.Succeeded)
                return;

            using (var assemblyCache = new CachedAssemblyResolver()) {
                assemblyCache.AddSearchDirectory(GetAssembliesDirectory());

                foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)) {
                    var assemblyPath = GetAssembliesDirectory() + "/" + assembly.name + ".dll";

                    var options = AssemblyPostProcessor.PatcherOptions.None;

                    var skipSymbols = (report.summary.options & BuildOptions.Development) == 0;
                    if (skipSymbols) {
                        options |= AssemblyPostProcessor.PatcherOptions.SkipSymbols;
                    }

                    AssemblyPostProcessor.Process(assemblyPath, assemblyCache, options);
                }
            }
        }
    }
}
