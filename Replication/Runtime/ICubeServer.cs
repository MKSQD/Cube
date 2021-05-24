using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeServer {
        IServerReplicaManager ReplicaManager { get; }
        ServerReactor Reactor { get; }
        IServerNetworkInterface NetworkInterface { get; }
        IWorld World { get; }
    }
}