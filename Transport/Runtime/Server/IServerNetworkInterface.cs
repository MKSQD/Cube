using System;

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
        
        void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity, Connection connection);
        BitStream Receive(out Connection connection);

        void Update();
    }
}
