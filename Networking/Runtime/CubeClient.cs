using Cube.Replication;
using Cube.Transport;

namespace Cube.Networking {
    public class CubeClient : ICubeClient {
        public IClientNetworkInterface networkInterface {
            get;
            internal set;
        }
        public ClientReactor reactor {
            get;
            internal set;
        }
        public IClientReplicaManager replicaManager {
            get;
            internal set;
        }
        public IWorld world {
            get;
            internal set;
        }

        public CubeClient(IWorld world, IClientNetworkInterface networkInterface) {
            this.world = world;
            this.networkInterface = networkInterface;
            reactor = new ClientReactor(networkInterface);
            replicaManager = new ClientReplicaManager(this, NetworkPrefabLookup.instance);
        }
        
        public void Update() {
            reactor.Update();
            replicaManager.Update();
            networkInterface.Update();
        }

        public void Shutdown() {
            networkInterface.Shutdown(0);
        }
    }
}