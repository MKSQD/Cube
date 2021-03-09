using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeServer {
        IServerReplicaManager replicaManager { get; }
        ServerReactor reactor { get; }
        IServerNetworkInterface networkInterface { get; }
        IWorld world { get; }
    }
}