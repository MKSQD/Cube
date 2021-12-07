using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Net;
using System.Net.Sockets;

namespace Cube.Transport {
    public class LiteNetClientNetworkInterface : IClientNetworkInterface, INetEventListener {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }
        public Action NetworkError { get; set; }
        public Action<BitReader> ReceivedPacket { get; set; }

        public bool IsConnected => true; // #todo

        readonly NetManager client;

        public LiteNetClientNetworkInterface() {
            client = new NetManager(this);
            client.ChannelsCount = 3;
            client.MaxConnectAttempts = 3;

#if UNITY_EDITOR
            client.DisconnectTimeout = 5000000;
#endif
            client.Start();
        }

        public void Connect(string address, ushort port) {
            client.Connect(address, port, "");
        }

        public void Connect(string address, ushort port, BitWriter hailMessage) {
            hailMessage.FlushBits();

            var msg = NetDataWriter.FromBytes(hailMessage.DataWritten.Slice(0, hailMessage.BytesWritten).ToArray(), 0, hailMessage.BytesWritten);
            client.Connect(address, port, msg);
        }

        public void Disconnect() {
            client.DisconnectAll();
        }

        public float GetRemoteTime(float time) {
            return time; // #todo
        }

        public void Send(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            client.FirstPeer.Send(bs.DataWritten, (byte)sequenceChannel, GetDeliveryMethod(reliablity));
        }

        public void Shutdown(uint blockDuration) {
            client.Stop();
        }

        public void Update() {
            client.PollEvents();
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

        Memory<uint> memory = new Memory<uint>(new uint[64]);
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