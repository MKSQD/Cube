using Cube.Transport;

namespace Cube.Replication {
    public interface IUnityServer {
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