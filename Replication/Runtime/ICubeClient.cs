using Cube.Transport;

namespace Cube.Replication {
    public interface ICubeClient {
        IClientReplicaManager replicaManager {
            get;
        }

        IClientReactor reactor {
            get;
        }

        IClientNetworkInterface networkInterface {
            get;
        }
    }
}