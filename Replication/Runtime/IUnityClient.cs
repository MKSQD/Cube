using Cube.Transport;

namespace Cube.Replication {
    public interface IUnityClient {
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