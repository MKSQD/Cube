using UnityEngine;

namespace Cube.Transport.LiteNet {
    public class LiteNetTransport : MonoBehaviour, ITransport {
        public IClientNetworkInterface CreateClient() => new LiteNetClientNetworkInterface();

        public IServerNetworkInterface CreateServer() => new LiteNetServerNetworkInterface();
    }
}