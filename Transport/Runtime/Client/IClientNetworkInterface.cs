namespace Cube.Transport {
    public interface IClientNetworkInterface {
        BitStreamPool bitStreamPool {
            get;
        }
        
        float GetRemoteTime(float time);
        
        /// <exception cref="ClientConnectionAttemptException">Throw on connection error</exception>
        void Connect(string address, ushort port);
        void Disconnect();

        void Update();

        void Shutdown(uint blockDuration);

        bool IsConnected();

        unsafe void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity);
        unsafe BitStream Receive();
    }
}

