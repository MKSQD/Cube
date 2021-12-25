using UnityEngine;

namespace Cube.Transport.LiteNet {
    public class LiteNetTransport : MonoBehaviour, ITransport {
        public IClientNetworkInterface CreateClient() => new LiteNetClientNetworkInterface();

        public IServerNetworkInterface CreateServer(int numMaxClients, SimulatedLagSettings lagSettings) => new LiteNetServerNetworkInterface(numMaxClients, lagSettings);
    }
}