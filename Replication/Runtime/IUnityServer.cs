using Cube.Transport;

namespace Cube.Replication {
    public interface IUnityServer {
#if SERVER
        IServerReplicaManager replicaManager {
            get;
        }

        IServerReactor reactor {
            get;
        }

        IServerNetworkInterface networkInterface {
            get;
        }
#endif
    }
}