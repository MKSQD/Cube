namespace Cube.Transport {
    public interface IServerNetworkInterface {
        BitStreamPool bitStreamPool {
            get;
        }

        bool isRunning {
            get;
        }
        
        void Shutdown();
        
        Connection[] GetConnections();
        
        void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity, Connection connection, int sequenceChannel = 0);
        void Broadcast(BitStream bs, PacketPriority priority, PacketReliability reliablity, int sequenceChannel = 0);
        BitStream Receive(out Connection connection);

        void Update();
    }
}
