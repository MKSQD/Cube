using System;

namespace Cube.Transport {
    public interface IClientNetworkInterface {
        Action ConnectionRequestAccepted { get; set; }
        Action<string> Disconnected { get; set; }
        Action NetworkError { get; set; }
        Action<BitReader> ReceivedPacket { get; set; }

        bool IsConnected { get; }

        float GetRemoteTime(float time);

        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address);
        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address, BitWriter hailMessage);
        void Disconnect();

        void Update();

        void Shutdown(uint blockDuration);

        void Send(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0);
    }
}

