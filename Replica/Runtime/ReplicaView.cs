using UnityEngine;
using System;
using System.Collections.Generic;
using Cube.Networking.Transport;

namespace Cube.Networking.Replicas {
#if SERVER
    /// <remarks>Available in: Editor/Server</remarks>
    [Serializable]
    [AddComponentMenu("Cube/ReplicaView")]
    public class ReplicaView : MonoBehaviour {
#if UNITY_EDITOR
        /// <summary>
        /// The view currently being debugged.
        /// </summary>
        public static ReplicaView debug;
#endif

        public struct UpdateInfo {
            public float lastUpdateTime;
            public float nextFullUpdateTime;
        }

        Connection _connection;
        public Connection connection {
            get { return _connection; }
            internal set { _connection = value; }
        }
        
        public bool ignoreReplicaPositionsForPriority = false;

        public Dictionary<Replica, UpdateInfo> replicaUpdateInfo = new Dictionary<Replica, UpdateInfo>();
    }
#endif
}
