using System;
using System.Collections.Generic;
using UnityEngine;

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

        ulong _bytesSent, _bytesReceived;
        uint _packetsSent, _packetsReceived;

        public LocalTransport Transport { get; private set; }

        readonly List<LocalClientNetworkInterface> _clients = new();

        public LocalServerNetworkInterface(LocalTransport transport) {
            Transport = transport;
            Transport.RunningServer = this;
        }

        public void BroadcastPacket(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            foreach (var c in _clients) {
                var br = new BitReader(bs);
                c.OnNetworkReceive(br);
            }
        }

        public void SendPacket(BitWriter bs, PacketReliability reliablity, Connection connection, int sequenceChannel = 0) {
            bs.FlushBits();

            foreach (var c in _clients) {
                if (c.Id != connection.id)
                    continue;

                _bytesSent += (ulong)bs.BytesWritten;
                ++_packetsSent;

                var br = new BitReader(bs);
                c.OnNetworkReceive(br);
                break;
            }
        }

        public void Shutdown() {
            Transport.RunningServer = null;
        }

        public void Update() {
#if UNITY_EDITOR
            TransportDebugger.CycleFrame();

            {
                var f = _bytesSent / Time.time;
                f /= 1024; // b -> kb
                var f2 = Mathf.RoundToInt(f * 100) * 0.01f;
                TransportDebugger.ReportStatistic($"out {_packetsSent} {f2}kb/s");
            }
            {
                var f = _bytesReceived / Time.time;
                f /= 1024; // b -> kb
                var f2 = Mathf.RoundToInt(f * 100) * 0.01f;
                TransportDebugger.ReportStatistic($"in {_packetsReceived} {f2}kb/s");
            }
#endif
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
            _bytesReceived += (ulong)bs.NumBytes;
            ++_packetsReceived;

            ReceivedPacket(bs, new Connection(id));
        }
    }
}
