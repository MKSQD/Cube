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
        public Action<BitStream> ReceivedPacket { get; set; }

        public bool IsConnected => true; // #todo

        readonly NetManager client;

        public LiteNetClientNetworkInterface() {
            client = new NetManager(this);
            client.Start();
        }

        public void Connect(string address, ushort port) {
            client.Connect(address, port, "");
        }

        public void Connect(string address, ushort port, BitStream hailMessage) {
            var msg = NetDataWriter.FromBytes(hailMessage.Data, 0, hailMessage.Length);
            client.Connect(address, port, msg);
        }

        public void Disconnect() {
            client.DisconnectAll();
        }

        public float GetRemoteTime(float time) {
            return time; // #todo
        }

        public void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity) {
            client.FirstPeer.Send(bs.Data, 0, bs.Length, GetDeliveryMethod(reliablity));
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
            throw new NotImplementedException();
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
            
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) {
            var bs = BitStream.CreateWithExistingBuffer(reader.RawData,
                     reader.UserDataOffset * 8,
                     reader.RawDataSize * 8);

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