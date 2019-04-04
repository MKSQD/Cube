using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Cube;

namespace Cube {

    /// <summary>
    /// Base class for build configurations
    /// </summary>
    abstract public class BuildConfiguration : ScriptableObject {
        /// <summary>Target location</summary>
        public string targetLocation;

        /// <summary>Target name</summary>
        public string targetName;

        /// <see cref="UnityEditor.BuildTargetGroup"/>
        public BuildTargetGroup targetGroup;

        /// <see cref="UnityEditor.BuildTarget"/>
        public BuildTarget buildTarget;

        /// <see cref="UnityEditor.BuildOptions.AcceptExternalModificationsToPlayer"/>
        public bool acceptExternalModificationsToPlayer;

        /// <see cref="UnityEditor.BuildOptions.Development"/>
        public bool development;

        /// <summary>
        /// The scenes to be included in the build. If empty, the currently open scene will
        //  be built. Paths are relative to the project folder (Assets/MyLevels/MyScene.unity).
        /// </summary>
        public SceneReference[] scenes;

        /// <remarks>
        /// Throw Exception to cancel build.
        /// </remarks>
        abstract public void OnPreProcessBuild();

        /// <remarks>
        /// Throw Exception to cancel build.
        /// </remarks>
        abstract public void OnPostProcessBuild(BuildReport report);

        public virtual BuildPlayerOptions GetBuildPlayerOptions() {
            var options = new BuildPlayerOptions();

            options.target = buildTarget;
            options.targetGroup = targetGroup;
            options.options = GetBuildOptions();
            options.locationPathName = targetLocation + "/" + targetName + ".exe";   //#TODO check + validate path

            //BuildPlayer expects paths relative to the project folder.
            options.scenes = SceneReference.ToStringArray(scenes);
            for (int i = 0; i < options.scenes.Length; i++)
                options.scenes[i] = "Assets/" + options.scenes[i] + ".unity";

            return options;
        }

        protected virtual BuildOptions GetBuildOptions() {
            var options = new BuildOptions();

            if (acceptExternalModificationsToPlayer) options |= BuildOptions.AcceptExternalModificationsToPlayer;
            if (development) options |= BuildOptions.Development;

            return options;
        }

        public virtual string GetAssembliesDirectory() {
            return targetLocation + "/" + targetName + "_Data/Managed";
        }
    }

}