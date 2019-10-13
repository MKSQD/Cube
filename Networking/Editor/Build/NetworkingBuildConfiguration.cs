using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using UnityEditor;
using Cube.Replication.Editor;

namespace Cube.Networking.Editor {
    /// <summary>
    /// Base class for build configurations
    /// </summary>
    [CreateAssetMenu(menuName = "Cube/BuildSystem/NetworkingBuildConfiguration")]
    public class NetworkingBuildConfiguration : BuildConfiguration {

        override public void OnPreProcessBuild() {}

        override public void OnPostProcessBuild(BuildReport report) {
            if (report.summary.result != BuildResult.Succeeded)
                return;

            var searchPathList = new List<string>();
            searchPathList.Add(GetAssembliesDirectory());

            foreach (var assembly in CompilationPipeline.GetAssemblies()) {
                if ((assembly.flags & AssemblyFlags.EditorAssembly) != 0)
                    continue;

                //#FIXME: Workaround because unity sucks (no way to check if PlayTest.dll)
                if (assembly.name == "Cube.Networking.Transport.RuntimeTests" || assembly.name == "Cube.Networking.RuntimeTests")
                    continue;

                var assemblyPath = GetAssembliesDirectory() + "/" + assembly.name + ".dll";

                var options = AssemblyPostProcessor.PatcherOptions.None;

                var skipSymbols = (report.summary.options & BuildOptions.Development) == 0;
                if(skipSymbols) {
                    options |= AssemblyPostProcessor.PatcherOptions.SkipSymbols;
                }

                AssemblyPostProcessor.Start(assemblyPath, searchPathList.ToArray(), options);
            }
        }        
    }
}
