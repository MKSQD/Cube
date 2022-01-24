using UnityEngine;

namespace Cube.Transport.LiteNet {
    [CreateAssetMenu(menuName = "Cube/LiteNetTransport")]
    public class LiteNetTransport : Transport {
        public ushort Port = 60000;
        public int MaxClients = 30;
        public SimulatedLagSettings LagSettings;

        public override IClientNetworkInterface CreateClient() => new LiteNetClientNetworkInterface(this);

        public override IServerNetworkInterface CreateServer() => new LiteNetServerNetworkInterface(this);
    }
}