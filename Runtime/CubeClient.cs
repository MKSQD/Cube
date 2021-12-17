using Cube.Replication;
using Cube.Transport;

namespace Cube {
    public class CubeClient : ICubeClient {
        public IClientNetworkInterface NetworkInterface { get; private set; }
        public ClientReactor Reactor { get; private set; }
        public IClientReplicaManager ReplicaManager { get; private set; }
        public IWorld World { get; private set; }

        public CubeClient(IWorld world, IClientNetworkInterface networkInterface) {
            World = world;
            NetworkInterface = networkInterface;
            Reactor = new ClientReactor(networkInterface);
            ReplicaManager = new ClientReplicaManager(this, NetworkPrefabLookup.Instance);
        }


        public void Update() {
            NetworkInterface.Update();
        }

        public void Tick() {
            ReplicaManager.Tick();
        }

        public void Shutdown() {
            NetworkInterface.Shutdown(0);
        }
    }
}