using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Cube.Networking {
    public class Build {
        public static bool isBuilding {
            get { return _currentConfiguration != null; }
        }

        static BuildConfiguration _currentConfiguration = null;
        public static bool currentConfiguration {
            get { return _currentConfiguration; }
        }

        public static void BuildWithConfiguration(string configurationName) {
            BuildWithConfiguration(TryGetConfiguration(configurationName));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="System.Exception">When build failes</exception>
        /// <param name="configuration"></param>
        public static void BuildWithConfiguration(BuildConfiguration configuration) {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            using (var locker = new ReloadAssembiesLocker()) {
                _currentConfiguration = configuration;
                using (var rollback = new Rollback(() => { _currentConfiguration = null; })) {
                    var options = configuration.GetBuildPlayerOptions();

                    configuration.OnPreProcessBuild();
                    var buildReport = BuildPipeline.BuildPlayer(options);
                    configuration.OnPostProcessBuild(buildReport);
                }
            }

            Debug.Log("*** Build success ***");
        }

        public static BuildConfiguration TryGetConfiguration(string name) {
            BuildConfiguration config = null;

            var result = AssetDatabase.FindAssets(name);
            var configList = new List<BuildConfiguration>();
            foreach (var guid in result) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BuildConfiguration>(path);

                if (asset == null || !path.EndsWith(name + ".asset"))
                    continue;

                configList.Add(asset);
            }

            if (configList.Count == 1)
                config = configList[0];
            else if (configList.Count > 1)
                throw new Exception("Two or more configurations with same name are not allowed.");

            return config;
        }

        static DateTime? GetFileTime(string path) {
            try {
                if (File.Exists(path))
                    return File.GetLastWriteTime(path);
            } catch (Exception) { }

            return null;
        }
    }

}

