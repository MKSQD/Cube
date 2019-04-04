using Cube.Networking.Transport;

namespace Cube.Networking.Replicas {
    public interface IUnityClient {
#if CLIENT
        IClientReplicaManager replicaManager {
            get;
        }

        IClientReactor reactor {
            get;
        }

        IClientNetworkInterface networkInterface {
            get;
        }
#endif
    }
}