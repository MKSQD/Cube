using System;

namespace Cube.Transport {
    public interface IClientNetworkInterface {
        Action ConnectionRequestAccepted { get; set; }
        Action<string> Disconnected { get; set; }
        Action NetworkError { get; set; }
        Action<BitStream> ReceivedPacket { get; set; }

        bool IsConnected { get; }

        float GetRemoteTime(float time);
        
        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address, ushort port);
        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address, ushort port, BitStream hailMessage);
        void Disconnect();

        void Update();

        void Shutdown(uint blockDuration);

        unsafe void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity);
    }
}

