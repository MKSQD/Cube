using UnityEngine;

namespace Cube.Replication {
    /// <summary>
    /// References to NetworkObject-derived instances can be used as RPC arguments.
    /// Note that only the reference, NOT the ScriptableObject content are sent.
    /// </summary>
    public class NetworkObject : ScriptableObject {
        [HideInInspector]
        public int networkAssetId = -1;
    }
}