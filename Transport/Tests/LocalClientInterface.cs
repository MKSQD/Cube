using System;
using System.Collections.Generic;

namespace Cube.Transport.Tests {
    public class LocalClientInterface : IClientNetworkInterface {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }
        public Action NetworkError { get; set; }
        public Action<BitStream> ReceivedPacket { get; set; }

        public LocalServerInterface server;
        public Connection connection = Connection.Invalid;

        public bool IsConnected => connection != Connection.Invalid;

        readonly Queue<BitStream> messageQueue = new Queue<BitStream>();


        public LocalClientInterface() { }

        public LocalClientInterface(LocalServerInterface server) {
            this.server = server;
            this.server.AddClient(this);
        }

        public float GetRemoteTime(float time) {
            throw new NotImplementedException();
        }

        public void Update() {
            ReceiveMessages();
            BitStreamPool.FrameReset();
        }

        void ReceiveMessages() {
            while (messageQueue.Count > 0) {
                var bs = messageQueue.Dequeue();
                ReceivedPacket(bs);
            }
        }

        public void Send(BitStream bs, PacketReliability reliablity) {
            if (!IsConnected)
                throw new Exception("Not connected.");

            server.EnqueueMessage(connection, bs);
        }

        public void Connect(string address, ushort port) {
            throw new Exception("Not required.");
        }

        public void Connect(string address, ushort port, BitStream hailMessage) {
            throw new Exception("Not required.");
        }

        public void Disconnect() {
            throw new Exception("Not required.");
        }

        public void Shutdown(uint blockDuration) {
            throw new Exception("Not required.");
        }

        #region TestInterface

        public void EnqueueMessage(BitStream bs) {
            messageQueue.Enqueue(bs);
        }

        public void SetServer(LocalServerInterface server) {
            this.server = server;
            this.server.AddClient(this);
        }

        #endregion

    }
}
