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
        public IClientReplicaManager ReplicaManager {
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
            ReplicaManager = new ClientReplicaManager(this, NetworkPrefabLookup.Instance);
        }

        public void Update() {
            ReplicaManager.Update();
            NetworkInterface.Update();
        }

        public void Shutdown() {
            NetworkInterface.Shutdown(0);
        }
    }
}