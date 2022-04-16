using Cube.Transport;
using UnityEngine;

namespace Cube.Replication {
    public interface ICubeServer {
        IServerReplicaManager ReplicaManager { get; }
        ServerReactor Reactor { get; }
        IServerNetworkInterface NetworkInterface { get; }
        Transform ReplicaParentTransform { get; }
        ServerReplicaManagerSettings ReplicaManagerSettings { get; }
    }
}