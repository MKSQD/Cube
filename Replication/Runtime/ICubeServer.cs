using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeServer {
        IServerReplicaManager replicaManager {
            get;
        }

        IServerReactor reactor {
            get;
        }

        IServerNetworkInterface networkInterface {
            get;
        }
    }
}