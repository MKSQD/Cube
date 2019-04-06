using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube.Networking {
    public class UnityClient : IUnityClient {
#if CLIENT
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

        public UnityClient(Transform clientTransform, ClientSimulatedLagSettings lagSettings) {
            networkInterface = new LidgrenClientNetworkInterface(lagSettings);
            reactor = new ClientReactor(networkInterface);
            replicaManager = new ClientReplicaManager(reactor, NetworkPrefabLookup.instance, clientTransform);
        }
        
        public void Update() {
            reactor.Update();
            replicaManager.Update();
            networkInterface.Update();
        }

        public void Shutdown() {
            networkInterface.Shutdown(0);
        }
#endif
    }
}