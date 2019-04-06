namespace Cube.Transport {
#if CLIENT
    public delegate void ClientMessageHandler(BitStream bs);

    public interface IClientReactor
    {
        IClientNetworkInterface networkInterface {
            get;
        }

        void AddHandler(byte id, ClientMessageHandler handler);

        void Update();
    }
#endif
}

