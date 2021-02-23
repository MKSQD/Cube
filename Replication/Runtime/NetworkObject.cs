using UnityEngine;

namespace Cube.Replication {
    public class NetworkObject : ScriptableObject {
        [Tooltip("Used when passing the NetworkObject as an RPC argument")]
        public int networkAssetId = -1;
    }
}