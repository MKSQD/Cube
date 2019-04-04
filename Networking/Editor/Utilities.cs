using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;

namespace Cube.Networking {
    //#TODO remove
    public class Utilities {
        public static string AbsolutePath(string path) {
            return Path.GetFullPath(path)
                .Replace("\\", "/");
        }

        public static string CacheDirectory() {
            return AbsolutePath(Application.dataPath + "/../.Cube.Networking/");
        }

        public static void CreateCacheDirectory() {
            Directory.CreateDirectory(Application.dataPath + "/../.Cube.Networking/");
        }
    }
}
