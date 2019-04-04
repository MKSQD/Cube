using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Networking.Replicas {
    public class ScenesInfo : ScriptableObject {
        [Serializable]
        public struct Entry {
            public byte id;
            public string scenePath;
        }

        public List<Entry> infos;

    }
}
