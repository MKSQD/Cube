using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Compilation;
using UnityEditor.Build.Reporting;
using Cube.Replication;
using UnityEditor;
using Cube.Replication.Editor;

namespace Cube.Networking.Editor {
    /// <summary>
    /// Base class for build configurations
    /// </summary>
    [CreateAssetMenu(menuName = "Cube/BuildSystem/NetworkingBuildConfiguration")]
    public class NetworkingBuildConfiguration : BuildConfiguration {
        public ApplicationType applicationType;

#region temp vars
        ScriptDefinitions.Snapshot _snapshot;
#endregion

        override public void OnPreProcessBuild() {
            var definitions = new ScriptDefinitions(targetGroup);
            _snapshot = definitions.TakeSnapshot();

            definitions.Set("CLIENT", (applicationType & ApplicationType.Client) != 0);
            definitions.Set("SERVER", (applicationType & ApplicationType.Server) != 0);

            definitions.Write();
        }

        override public void OnPostProcessBuild(BuildReport report) {
            var definitions = new ScriptDefinitions(targetGroup);
            definitions.SetSnapshot(_snapshot);
            definitions.Write();
            
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

                var assemblyPath = GetAssembliesDirectory() + "/" + assembly.name + ".dll";  //#TODO check targetLocation path
                var withSymbols = (report.summary.options & BuildOptions.Development) != 0;

                AssemblyPostProcessor.Start(applicationType, assemblyPath, searchPathList.ToArray(), withSymbols);
            }
        }        
    }
}
