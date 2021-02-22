using System;
using UnityEngine;

namespace Cube.Replication {
    [Serializable]
    public class NetworkObject : ScriptableObject {
        [Tooltip("Used when passing the NetworkObject as an RPC argument")]
        public int networkAssetId = -1;
    }
}