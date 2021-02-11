using System;

namespace Cube.Transport {
    public interface IClientNetworkInterface {
        Action ConnectionRequestAccepted { get; set; }
        Action<string> Disconnected { get; set; }

        BitStreamPool bitStreamPool {
            get;
        }
        
        float GetRemoteTime(float time);
        
        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address, ushort port);
        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address, ushort port, BitStream hailMessage);
        void Disconnect();

        void Update();

        void Shutdown(uint blockDuration);

        bool IsConnected();

        unsafe void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity);
        unsafe BitStream Receive();
    }
}

