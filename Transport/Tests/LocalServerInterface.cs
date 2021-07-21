using System;
using System.Collections.Generic;

namespace Cube.Transport.Tests {
    public class LocalServerInterface : IServerNetworkInterface {
        class Message {
            public Connection connection;
            public BitStream bs;
        }

        public Func<BitStream, ApprovalResult> ApproveConnection { get; set; }
        public Action<Connection> NewConnectionEstablished { get; set; }
        public Action NetworkError { get; set; }
        public Action<Connection> DisconnectNotification { get; set; }
        public Action<BitStream, Connection> ReceivedPacket { get; set; }

        public List<LocalClientInterface> clients = new List<LocalClientInterface>();

        ulong nextConnectionId = 0;
        readonly Queue<Message> messageQueue = new Queue<Message>();

        public bool IsRunning => true;

        public void Update() {
            ReceiveMessages();
            BitStreamPool.FrameReset();
        }

        void ReceiveMessages() {
            while (messageQueue.Count > 0) {
                var msg = messageQueue.Dequeue();
                ReceivedPacket.Invoke(msg.bs, msg.connection);
            }
        }

        public void Send(BitStream bs, PacketReliability reliablity, Connection connection, int sequenceChannel) {
            LocalClientInterface targetClient = null;

            foreach (var client in clients) {
                if (client.connection == connection) {
                    targetClient = client;
                    break;
                }
            }

            if (targetClient == null)
                throw new Exception("Client not found.");

            targetClient.EnqueueMessage(bs);
        }

        public void BroadcastBitStream(BitStream bs, PacketReliability reliablity, int sequenceChannel) {
            foreach (var client in clients) {
                client.EnqueueMessage(bs);
            }
        }

        public void Shutdown() {
            throw new Exception("Not required.");
        }

        public void Start() {
            throw new Exception("Not required.");
        }

        #region TestInterface

        public void AddClient(LocalClientInterface client) {
            client.connection = new Connection(nextConnectionId);
            nextConnectionId++;

            clients.Add(client);
        }

        public void EnqueueMessage(Connection connection, BitStream bs) {
            var message = new Message();
            message.connection = connection;
            message.bs = bs;
            messageQueue.Enqueue(message);
        }

        #endregion

    }
}
