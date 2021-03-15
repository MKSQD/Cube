using System;

namespace Cube.Transport {
    public struct ApprovalResult {
        public bool Approved;
        public string DenialReason;
    }

    public interface IServerNetworkInterface {
        Func<BitStream, ApprovalResult> ApproveConnection { get; set; }
        Action<Connection> NewConnectionEstablished { get; set; }
        Action NetworkError { get; set; }
        Action<Connection> DisconnectNotification { get; set; }
        Action<BitStream, Connection> ReceivedPacket { get; set; }

        bool IsRunning { get; }
        
        void SendBitStream(BitStream bs, PacketPriority priority, PacketReliability reliablity, Connection connection, int sequenceChannel = 0);
        void BroadcastBitStream(BitStream bs, PacketPriority priority, PacketReliability reliablity, int sequenceChannel = 0);

        void Update();

        void Shutdown();
    }
}
