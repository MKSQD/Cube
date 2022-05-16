using System;
using System.Collections.Generic;

namespace Cube.Transport.Local {
    public sealed class LocalServerNetworkInterface : IServerNetworkInterface {
        public Func<BitReader, ApprovalResult> ApproveConnection { get; set; }
        public Action<Connection> NewConnectionEstablished { get; set; }
        public Action NetworkError { get; set; }
        public Action<Connection> DisconnectNotification { get; set; }
        public Action<BitReader, Connection> ReceivedPacket { get; set; }

        public bool IsRunning => true;

        public int NumClientsConnected => _clients.Count;
        public int NumMaxClients => 0;

        public LocalTransport Transport { get; private set; }

        readonly List<LocalClientNetworkInterface> _clients = new();

        public LocalServerNetworkInterface(LocalTransport transport) {
            Transport = transport;
            Transport.RunningServer = this;
        }

        public void BroadcastBitStream(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            foreach (var c in _clients) {
                var br = new BitReader(bs);
                c.OnNetworkReceive(br);
            }
        }

        public void Send(BitWriter bs, PacketReliability reliablity, Connection connection, int sequenceChannel = 0) {
            bs.FlushBits();

            foreach (var c in _clients) {
                if (c.Id != connection.id)
                    continue;

                var br = new BitReader(bs);
                c.OnNetworkReceive(br);
                break;
            }
        }

        public void Shutdown() {
            Transport.RunningServer = null;
        }

        public void Update() {
        }

        public void OnPeerConnected(LocalClientNetworkInterface client) {
            _clients.Add(client);
            NewConnectionEstablished.Invoke(new Connection(client.Id));
        }

        public void OnPeerDisconnected(LocalClientNetworkInterface client) {
            DisconnectNotification(new Connection(client.Id));
            _clients.Remove(client);
        }

        public void OnNetworkReceive(ulong id, BitReader bs) {
            ReceivedPacket(bs, new Connection(id));
        }
    }
}
