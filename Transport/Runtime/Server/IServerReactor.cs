namespace Cube.Transport {
#if SERVER
    public delegate void ServerMessageHandler(Connection connection, BitStream bs);

    public interface IServerReactor {
        IServerNetworkInterface networkInterface {
            get;
        }

        void AddHandler(byte id, ServerMessageHandler handler);

        void Update();
    }
#endif
}
