using Cube.Transport;

namespace Cube.Replication {
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