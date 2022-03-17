using System;

namespace Cube.Transport.Local {
    public class LocalClientNetworkInterface : IClientNetworkInterface {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }
        public Action NetworkError { get; set; }
        public Action<BitReader> ReceivedPacket { get; set; }

        public bool IsConnected => true; // #todo
        public float Ping => 0;

        public LocalTransport Transport { get; private set; }

        public readonly ulong Id;

        public LocalClientNetworkInterface(LocalTransport transport) {
            Transport = transport;
            Id = Transport.NextClientIdx++;
        }

        public void Connect(string address) {
            Transport.RunningServer.OnPeerConnected(this);
        }

        public void Connect(string address, BitWriter hailMessage) {
            hailMessage.FlushBits();

            Transport.RunningServer.OnPeerConnected(this);
            ConnectionRequestAccepted();
        }

        public void Disconnect() {
            Transport.RunningServer.OnPeerDisconnected(this);
        }

        public float GetRemoteTime(float time) => time;

        public void Send(BitWriter bs, PacketReliability reliablity, int sequenceChannel = 0) {
            bs.FlushBits();

            var br = new BitReader(bs);
            Transport.RunningServer.OnNetworkReceive(Id, br);
        }

        public void Shutdown(uint blockDuration) {
        }

        public void Update() {
        }

        public void OnNetworkReceive(BitReader bs) {
            ReceivedPacket(bs);
        }
    }
}