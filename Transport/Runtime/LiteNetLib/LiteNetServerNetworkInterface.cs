using LiteNetLib;
using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Cube.Transport {
    public sealed class LiteNetServerNetworkInterface : IServerNetworkInterface, INetEventListener {
        public Func<BitReader, ApprovalResult> ApproveConnection { get; set; }
        public Action<Connection> NewConnectionEstablished { get; set; }
        public Action NetworkError { get; set; }
        public Action<Connection> DisconnectNotification { get; set; }
        public Action<BitReader, Connection> ReceivedPacket { get; set; }

        public bool IsRunning => server.IsRunning;

        readonly NetManager server;

        public LiteNetServerNetworkInterface(ushort port) {
            server = new NetManager(this);
            server.ChannelsCount = 4;

#if UNITY_EDITOR
            server.EnableStatistics = true;
            server.DisconnectTimeout = 5000000;
#endif

            server.Start(port);
        }

        public void BroadcastBitStream(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            // #todo Incomplete LiteNet support for Span :(
            server.SendToAll(bs.DataWritten.ToArray(), (byte)sequenceChannel, GetDeliveryMethod(reliablity));
        }

        public void Send(BitWriter bs, PacketReliability reliablity, Connection connection, int sequenceChannel = 0) {
            bs.FlushBits();

            var peer = server.GetPeerById((int)connection.id);
            peer.Send(bs.DataWritten, (byte)sequenceChannel, GetDeliveryMethod(reliablity));
        }

        public void Shutdown() {
            server.Stop();
        }

        public void Update() {
            server.PollEvents();

#if UNITY_EDITOR
            TransportDebugger.CycleFrame();

            {
                var f = server.Statistics.BytesSent / Time.time;
                f /= 1024; // b -> kb
                var f2 = Mathf.RoundToInt(f * 100) * 0.01f;

                TransportDebugger.ReportStatistic($"out {server.Statistics.PacketsSent} {f2}k/s");
            }
            {
                var f = server.Statistics.BytesReceived / Time.time;
                f /= 1024; // b -> kb
                var f2 = Mathf.RoundToInt(f * 100) * 0.01f;

                TransportDebugger.ReportStatistic($"in {server.Statistics.PacketsSent} {f2}k/s");
            }
#endif
        }

        public void OnPeerConnected(NetPeer peer) {
            NewConnectionEstablished.Invoke(new Connection((ulong)peer.Id));
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
