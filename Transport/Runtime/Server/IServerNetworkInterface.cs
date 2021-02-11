using System;

namespace Cube.Transport {
    public struct ApprovalResult {
        public bool Approved;
        public string DenialReason;
    }

    public interface IServerNetworkInterface {
        Func<BitStream, ApprovalResult> ApproveConnection { get; set; }
        Action<Connection> NewConnectionEstablished { get; set; }
        Action<Connection> DisconnectNotification { get; set; }

        BitStreamPool bitStreamPool {
            get;
        }

        bool isRunning {
            get;
        }
        
        void Shutdown();
        
        Connection[] GetConnections();
        
        void SendBitStream(BitStream bs, PacketPriority priority, PacketReliability reliablity, Connection connection, int sequenceChannel = 0);
        void BroadcastBitStream(BitStream bs, PacketPriority priority, PacketReliability reliablity, int sequenceChannel = 0);
        BitStream Receive(out Connection connection);

        void Update();
    }
}
