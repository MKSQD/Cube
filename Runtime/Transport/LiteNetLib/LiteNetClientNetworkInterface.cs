using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Cube.Transport.LiteNet {
    public class LiteNetClientNetworkInterface : IClientNetworkInterface, INetEventListener {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }
        public Action NetworkError { get; set; }
        public Action<BitReader> ReceivedPacket { get; set; }

        public bool IsConnected => true; // #todo
        public float Ping => _client.FirstPeer.Ping * 0.001f;

        public LiteNetTransport Transport { get; private set; }

        readonly NetManager _client;

        public LiteNetClientNetworkInterface(LiteNetTransport transport) {
            Transport = transport;

            _client = new NetManager(this);
            _client.ChannelsCount = 5;
            _client.MaxConnectAttempts = 3;
            _client.NatPunchEnabled = true;

#if UNITY_EDITOR
            _client.DisconnectTimeout = 5000000;
#endif
            _client.Start();
        }

        public void Connect(string address) {
            _client.Connect(address, Transport.Port, "");
        }

        public void Connect(string address, BitWriter hailMessage) {
            hailMessage.FlushBits();

            var msg = NetDataWriter.FromBytes(hailMessage.DataWritten[..hailMessage.BytesWritten].ToArray(), 0, hailMessage.BytesWritten);
            _client.Connect(address, Transport.Port, msg);
        }

        public void Disconnect() {
            _client.DisconnectAll();
        }

        public float GetRemoteTime(float time) {
            return time; // #todo
        }

        public void Send(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            _client.FirstPeer.Send(bs.DataWritten, (byte)sequenceChannel, GetDeliveryMethod(reliablity));
        }

        public void Shutdown(uint blockDuration) {
            _client.Stop();
        }

        public void Update() {
            _client.PollEvents();
        }

        public void OnPeerConnected(NetPeer peer) {
            ConnectionRequestAccepted();
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
            Disconnected(disconnectInfo.Reason.ToString());
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
            NetworkError();
        }

        readonly Memory<uint> memory = new(new uint[512]);
        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
            var span = new ReadOnlySpan<byte>(reader.RawData, reader.UserDataOffset, reader.UserDataSize);
            var bs = new BitReader(span, memory);
            ReceivedPacket(bs);

            reader.Recycle();
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {
        }

        public void OnConnectionRequest(ConnectionRequest request) {
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