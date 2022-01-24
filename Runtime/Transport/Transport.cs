using UnityEngine;

namespace Cube.Transport {
    public interface ITransport {
        IClientNetworkInterface CreateClient();
        IServerNetworkInterface CreateServer();
    }

    public abstract class Transport : ScriptableObject, ITransport {
        public abstract IClientNetworkInterface CreateClient();
        public abstract IServerNetworkInterface CreateServer();
    }
}