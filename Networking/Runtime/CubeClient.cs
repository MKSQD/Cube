using Cube.Replication;
using Cube.Transport;

namespace Cube.Networking {
    public class CubeClient : ICubeClient {
        public IClientNetworkInterface NetworkInterface {
            get;
            internal set;
        }
        public ClientReactor Reactor {
            get;
            internal set;
        }
        public IClientReplicaManager replicaManager {
            get;
            internal set;
        }
        public IWorld World {
            get;
            internal set;
        }

        public CubeClient(IWorld world, IClientNetworkInterface networkInterface) {
            this.World = world;
            this.NetworkInterface = networkInterface;
            Reactor = new ClientReactor(networkInterface);
            replicaManager = new ClientReplicaManager(this, NetworkPrefabLookup.Instance);
        }

        public void Update() {
            replicaManager.Update();
            NetworkInterface.Update();
        }

        public void Shutdown() {
            NetworkInterface.Shutdown(0);
        }
    }
}