using System;

namespace Cube.Transport {
    public struct ApprovalResult {
        public bool Approved;
        public string DenialReason;
    }

    public interface IServerNetworkInterface {
        /// Called before we establish a connection with a client.
        /// The passed BitStream is the hello message from the client.
        /// This can be used to check the client version, password, current game state, ...
        Func<BitReader, ApprovalResult> ApproveConnection { get; set; }

        /// Client with Connection connected to us.
        Action<Connection> NewConnectionEstablished { get; set; }

        Action NetworkError { get; set; }

        /// Client with Connection disconnected.
        Action<Connection> DisconnectNotification { get; set; }

        /// Received a user-defined, not Cube internal packet from Connection.
        Action<BitReader, Connection> ReceivedPacket { get; set; }

        bool IsRunning { get; }
        int NumClientsConnected { get; }
        int NumMaxClients { get; }

        void Start(ushort port);

        void Send(BitWriter bs, PacketReliability reliablity, Connection connection, int sequenceChannel = 0);
        void BroadcastBitStream(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0);

        void Update();

        void Shutdown();
    }
}
