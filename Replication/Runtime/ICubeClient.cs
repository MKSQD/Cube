using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeClient {
        IClientReplicaManager replicaManager { get; }
        ClientReactor reactor { get; }
        IClientNetworkInterface networkInterface { get; }
        IWorld world { get; }
    }
}