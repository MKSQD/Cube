using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;

namespace Cube.Networking {

    /// <summary>
    /// Base class for build configurations
    /// </summary>
    abstract public class BuildConfiguration : ScriptableObject {
        [Header("Target")]
        /// <summary>Target location</summary>
        public string targetLocation;

        /// <summary>Target name</summary>
        public string targetName;

        /// <see cref="UnityEditor.BuildTargetGroup"/>
        public BuildTargetGroup targetGroup;

        /// <see cref="UnityEditor.BuildTarget"/>
        public BuildTarget buildTarget;

        [Header("Options")]
        /// <see cref="UnityEditor.BuildOptions.Development"/>
        public bool development;

        /// <see cref="UnityEditor.BuildOptions.AutoRunPlayer"/>
        public bool autoRunPlayer;

        /// <remarks>
        /// Throw Exception to cancel build.
        /// </remarks>
        abstract public void OnPreProcessBuild();

        /// <remarks>
        /// Throw Exception to cancel build.
        /// </remarks>
        abstract public void OnPostProcessBuild(BuildReport report);

        public virtual BuildPlayerOptions GetBuildPlayerOptions() {
            var options = new BuildPlayerOptions {
                target = buildTarget,
                targetGroup = targetGroup,
                options = GetBuildOptions(),
                locationPathName = targetLocation + "/" + targetName + ".exe",   //#TODO check + validate path

                //BuildPlayer expects paths relative to the project folder.
                scenes = EditorBuildSettings.scenes.Select(scene => scene.path).ToArray()
            };
//             for (int i = 0; i < options.scenes.Length; i++)
//                 options.scenes[i] = "Assets/" + options.scenes[i] + ".unity";

            return options;
        }

        protected virtual BuildOptions GetBuildOptions() {
            var options = new BuildOptions();

            if (development) options |= BuildOptions.Development;
            if (autoRunPlayer) options |= BuildOptions.AutoRunPlayer;

            return options;
        }

        public virtual string GetAssembliesDirectory() {
            return targetLocation + "/" + targetName + "_Data/Managed";
        }
    }

}