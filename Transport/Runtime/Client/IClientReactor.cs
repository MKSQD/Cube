namespace Cube.Transport {
    public delegate void ClientMessageHandler(BitStream bs);

    public interface IClientReactor
    {
        IClientNetworkInterface networkInterface {
            get;
        }

        void AddHandler(byte id, ClientMessageHandler handler);
        void RemoveHandler(byte id, ClientMessageHandler handler);

        void Update();
    }
}

