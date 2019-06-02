using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Cube.Transport.Tests {
    public class LocalClientInterface : IClientNetworkInterface {
        BitStreamPool _bitStreamPool = new BitStreamPool();
        public BitStreamPool bitStreamPool {
            get { return _bitStreamPool; }
        }

        public LocalServerInterface server;
        public Connection connection = Connection.Invalid;

        Queue<BitStream> messageQueue = new Queue<BitStream>();

        public LocalClientInterface() {}

        public LocalClientInterface(LocalServerInterface server) {
            this.server = server;
            this.server.AddClient(this);
        }
        
        public float GetRemoteTime(float time) {
            throw new NotImplementedException();
        }

        public bool IsConnected() {
            return connection != Connection.Invalid;
        }

        public void Update() {
            bitStreamPool.FrameReset();
        }

        public BitStream Receive() {
            if (messageQueue.Count == 0)
                return null;

            return messageQueue.Dequeue();
        }

        public void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity) {
            if (!IsConnected())
                throw new Exception("Not connected.");

            server.EnqueueMessage(connection, bs);
        }
        
        public void Connect(string address, ushort port) {
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
