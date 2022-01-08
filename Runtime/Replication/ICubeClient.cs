using Cube.Transport;
using UnityEngine;

namespace Cube.Replication {
    public interface ICubeClient {
        IClientReplicaManager ReplicaManager { get; }
        ClientReactor Reactor { get; }
        IClientNetworkInterface NetworkInterface { get; }
        Transform ReplicaParentTransform { get; }
    }
}