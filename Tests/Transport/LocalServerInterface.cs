using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport.Tests {
    public class LocalTransport : MonoBehaviour, ITransport {
        public IClientNetworkInterface CreateClient() => new LocalClientInterface();

        public IServerNetworkInterface CreateServer(int numMaxClients, SimulatedLagSettings lagSettings) => new LocalServerInterface();
    }

    public class LocalServerInterface : IServerNetworkInterface {
        class Message {
            public Connection connection;
            public byte[] data;
        }

        public Func<BitReader, ApprovalResult> ApproveConnection { get; set; }
        public Action<Connection> NewConnectionEstablished { get; set; }
        public Action NetworkError { get; set; }
        public Action<Connection> DisconnectNotification { get; set; }
        public Action<BitReader, Connection> ReceivedPacket { get; set; }

        public List<LocalClientInterface> clients = new List<LocalClientInterface>();

        ulong nextConnectionId = 0;
        readonly Queue<Message> messageQueue = new Queue<Message>();

        public bool IsRunning => true;

        public int NumClientsConnected => clients.Count;

        public int NumMaxClients => 30;

        public void Start(ushort port) {
        }

        public void Update() {
            ReceiveMessages();
        }

        Memory<uint> memory = new Memory<uint>(new uint[64]);
        void ReceiveMessages() {
            while (messageQueue.Count > 0) {
                var msg = messageQueue.Dequeue();
                var bs = new BitReader(msg.data, memory);
                ReceivedPacket.Invoke(bs, msg.connection);
            }
        }

        public void Send(BitWriter bs, PacketReliability reliablity, Connection connection, int sequenceChannel) {
            bs.FlushBits();

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

        public void BroadcastBitStream(BitWriter bs, PacketReliability reliablity, int sequenceChannel) {
            bs.FlushBits();

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

        public void EnqueueMessage(Connection connection, BitWriter bs) {
            bs.FlushBits();

            var message = new Message();
            message.connection = connection;
            message.data = bs.DataWritten.ToArray();
            messageQueue.Enqueue(message);
        }
        #endregion

    }
}
