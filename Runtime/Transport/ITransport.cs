namespace Cube.Transport {
    public interface ITransport {
        IClientNetworkInterface CreateClient();
        IServerNetworkInterface CreateServer(int numMaxClients, SimulatedLagSettings lagSettings);
    }
}