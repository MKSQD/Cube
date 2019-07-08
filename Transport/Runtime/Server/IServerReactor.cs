namespace Cube.Transport {
    public delegate void ServerMessageHandler(Connection connection, BitStream bs);

    public interface IServerReactor {
        void AddMessageHandler(byte id, ServerMessageHandler handler);
        void RemoveMessageHandler(byte id, ServerMessageHandler handler);

        void Update();
    }
}
