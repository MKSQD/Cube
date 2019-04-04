using Cube.Networking.Transport;

namespace Cube.Networking.Replicas {
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