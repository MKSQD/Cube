using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeClient {
        IClientReplicaManager replicaManager { get; }
        ClientReactor Reactor { get; }
        IClientNetworkInterface NetworkInterface { get; }
        IWorld World { get; }
    }
}