using Cube.Replication;
using Cube.Transport;

namespace Cube.Networking {
    public class CubeClient : ICubeClient {
        public bool interpolate = true;

        public IClientNetworkInterface networkInterface {
            get;
            internal set;
        }
        
        public IClientReactor reactor {
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

        public CubeClient(IWorld world, ClientSimulatedLagSettings lagSettings) {
            this.world = world;
            networkInterface = new LidgrenClientNetworkInterface(lagSettings);
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