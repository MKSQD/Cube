using System;
using System.Collections.Generic;

namespace Cube.Transport.Tests {
    public class LocalClientInterface : IClientNetworkInterface {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }
        public Action NetworkError { get; set; }
        public Action<BitReader> ReceivedPacket { get; set; }

        public LocalServerInterface server;
        public Connection connection = Connection.Invalid;

        public bool IsConnected => connection != Connection.Invalid;

        readonly Queue<byte[]> messageQueue = new Queue<byte[]>();


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
        }

        Memory<uint> memory = new Memory<uint>(new uint[64]);
        void ReceiveMessages() {
            while (messageQueue.Count > 0) {
                var data = messageQueue.Dequeue();
                var bs = new BitReader(data, memory);
                ReceivedPacket(bs);
            }
        }

        public void Send(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            if (!IsConnected)
                throw new Exception("Not connected");

            server.EnqueueMessage(connection, bs);
        }

        public void Connect(string address) {
            throw new Exception("Not required");
        }

        public void Connect(string address, BitWriter hailMessage) {
            throw new Exception("Not required");
        }

        public void Disconnect() {
            throw new Exception("Not required");
        }

        public void Shutdown(uint blockDuration) {
            throw new Exception("Not required");
        }

        #region TestInterface

        public void EnqueueMessage(BitWriter bs) {
            bs.FlushBits();

            messageQueue.Enqueue(bs.DataWritten.ToArray());
        }

        public void SetServer(LocalServerInterface server) {
            this.server = server;
            this.server.AddClient(this);
        }

        #endregion

    }
}
