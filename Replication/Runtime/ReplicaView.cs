using UnityEngine;
using System;
using System.Collections.Generic;
using Cube.Transport;

namespace Cube.Replication {
#if SERVER
    /// <remarks>Available in: Editor/Server</remarks>
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
        public struct UpdateInfo {
            public float lastUpdateTime;
            public float nextFullUpdateTime;
        }

#if UNITY_EDITOR
        /// <summary>
        /// The view currently being debugged.
        /// </summary>
        public static ReplicaView debug;
#endif
        
        public Connection connection;
        public bool ignoreReplicaPositionsForPriority = false;

        public Dictionary<Replica, UpdateInfo> replicaUpdateInfo = new Dictionary<Replica, UpdateInfo>();
    }
#endif
}
