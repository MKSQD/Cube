using UnityEngine;

namespace Cube.Transport.Local {
    [CreateAssetMenu(menuName = "Cube/LocalTransport")]
    public class LocalTransport : Transport {
        [HideInInspector]
        public LocalServerNetworkInterface RunningServer;
        [HideInInspector]
        public ulong NextClientIdx = 0;

        public override IClientNetworkInterface CreateClient() => new LocalClientNetworkInterface(this);

        public override IServerNetworkInterface CreateServer() => new LocalServerNetworkInterface(this);
    }
}