using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeClient {
        ClientReplicaManager ReplicaManager { get; }
        ClientReactor Reactor { get; }
        IClientNetworkInterface NetworkInterface { get; }
    }
}