using Unity.Collections;
using UnityEngine;

namespace Cube.Replication {
    /// <summary>
    /// References to NetworkObject-derived instances can be used as RPC arguments.
    /// Note that only the reference, NOT the ScriptableObject contents are sent.
    /// </summary>
    public class NetworkObject : ScriptableObject {
        [ReadOnly]
        public int NetworkAssetId = -1;
    }
}