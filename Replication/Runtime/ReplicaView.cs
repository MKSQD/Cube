using UnityEngine;
using System;
using System.Collections.Generic;
using Cube.Transport;

namespace Cube.Replication {    /// <remarks>Available in: Editor/Server</remarks>
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
#if SERVER
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
        
        [HideInInspector]
        public Connection connection;
        public bool ignoreReplicaPositionsForPriority = false;

        /// <summary>
        /// If true this ReplicaView is ignored. Will be set automatically.
        /// </summary>
        public bool isLoadingLevel;

        public Dictionary<Replica, UpdateInfo> replicaUpdateInfo = new Dictionary<Replica, UpdateInfo>();
#endif
    }
}
