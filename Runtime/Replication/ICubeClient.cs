using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeClient {
        IClientReplicaManager ReplicaManager { get; }
        ClientReactor Reactor { get; }
        IClientNetworkInterface NetworkInterface { get; }
        IWorld World { get; }
    }
}