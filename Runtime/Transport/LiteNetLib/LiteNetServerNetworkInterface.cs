using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using UnityEngine;

namespace Cube.Transport.LiteNet {
    public sealed class LiteNetServerNetworkInterface : IServerNetworkInterface, INetEventListener {
        public Func<BitReader, ApprovalResult> ApproveConnection { get; set; }
        public Action<Connection> NewConnectionEstablished { get; set; }
        public Action NetworkError { get; set; }
        public Action<Connection> DisconnectNotification { get; set; }
        public Action<BitReader, Connection> ReceivedPacket { get; set; }

        public bool IsRunning => _server.IsRunning;

        public int NumClientsConnected => _server.ConnectedPeersCount;
        public int NumMaxClients => Transport.MaxClients;

        public LiteNetTransport Transport { get; private set; }

        readonly NetManager _server;

        public LiteNetServerNetworkInterface(LiteNetTransport transport) {
            Transport = transport;

            _server = new NetManager(this);
            _server.ChannelsCount = 4;

#if UNITY_EDITOR
            _server.EnableStatistics = true;
            _server.DisconnectTimeout = 5000000;

            if (transport.LagSettings.enabled) {
                _server.SimulatePacketLoss = true;
                _server.SimulateLatency = true;
                _server.SimulationMinLatency = transport.LagSettings.minimumLatencyMs;
                _server.SimulationMaxLatency = transport.LagSettings.minimumLatencyMs + transport.LagSettings.additionalRandomLatencyMs;
                _server.SimulationPacketLossChance = transport.LagSettings.simulatedLossPercent;
            }
#endif

            _server.Start(transport.Port);
        }

        public void BroadcastPacket(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            // #todo Incomplete LiteNet support for Span :(
            _server.SendToAll(bs.DataWritten.ToArray(), (byte)sequenceChannel, GetDeliveryMethod(reliablity));
        }

        public void SendPacket(BitWriter bs, PacketReliability reliablity, Connection connection, int sequenceChannel = 0) {
            bs.FlushBits();

            var peer = _server.GetPeerById((int)connection.id);
            peer.Send(bs.DataWritten, (byte)sequenceChannel, GetDeliveryMethod(reliablity));
        }

        public void Shutdown() {
            _server.Stop();
        }

        public void Update() {
            _server.PollEvents();

#if UNITY_EDITOR
            TransportDebugger.CycleFrame();

            {
                var f = _server.Statistics.BytesSent / Time.time;
                f /= 1024; // b -> kb
                var f2 = Mathf.RoundToInt(f * 100) * 0.01f;

                TransportDebugger.ReportStatistic($"out {_server.Statistics.PacketsSent} {f2}kb/s");
            }
            {
                var f = _server.Statistics.BytesReceived / Time.time;
                f /= 1024; // b -> kb
                var f2 = Mathf.RoundToInt(f * 100) * 0.01f;

                TransportDebugger.ReportStatistic($"in {_server.Statistics.PacketsSent} {f2}kb/s");
            }
#endif
        }

        public void OnPeerConnected(NetPeer peer) {
            NewConnectionEstablished(new Connection((ulong)peer.Id));
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
            DisconnectNotification(new Connection((ulong)peer.Id));
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
            NetworkError();
        }

        Memory<uint> memory = new Memory<uint>(new uint[64]);
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
            var span = new ReadOnlySpan<byte>(reader.RawData, reader.UserDataOffset, reader.UserDataSize);
            var bs = new BitReader(span, memory);

            ReceivedPacket(bs, new Connection((ulong)peer.Id));

            reader.Recycle();
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {
        }

        public void OnConnectionRequest(ConnectionRequest request) {
            if (NumClientsConnected >= Transport.MaxClients) {
                request.Reject();
                return;
            }

            var reader = request.Data;

            var span = new ReadOnlySpan<byte>(reader.RawData, reader.UserDataOffset, reader.UserDataSize);
            var bs = new BitReader(span, memory);

            var approvalResult = ApproveConnection.Invoke(bs);
            if (!approvalResult.Approved) {
                Debug.Log($"[Server] Connection denied ({approvalResult.DenialReason})");
                var deniedBs = new BitWriter();
                deniedBs.WriteString(approvalResult.DenialReason);
                request.Reject(deniedBs.DataWritten.ToArray(), 0, deniedBs.BytesWritten);
                return;
            }

            Debug.Log("[Server] Connection approved");
            request.Accept();
        }

        static DeliveryMethod GetDeliveryMethod(PacketReliability reliability) {
            return reliability switch {
                PacketReliability.Unreliable => DeliveryMethod.Unreliable,
                PacketReliability.UnreliableSequenced => DeliveryMethod.Sequenced,
                PacketReliability.ReliableUnordered => DeliveryMethod.ReliableUnordered,
                PacketReliability.ReliableOrdered => DeliveryMethod.ReliableOrdered,
                PacketReliability.ReliableSequenced => DeliveryMethod.ReliableSequenced,
                _ => throw new ArgumentException("reliability"),
            };
        }
    }
}
