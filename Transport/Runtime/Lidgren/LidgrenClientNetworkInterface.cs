using Lidgren.Network;
using System;
using UnityEngine;

namespace Cube.Transport {
    public class LidgrenClientNetworkInterface : IClientNetworkInterface {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }
        public Action NetworkError { get; set; }
        public Action<BitStream> ReceivedPacket { get; set; }

        public bool IsConnected => connection != null && connection.Status == NetConnectionStatus.Connected;

        NetClient client;
        NetConnection connection;

        public LidgrenClientNetworkInterface(SimulatedLagSettings lagSettings) {
            var config = new NetPeerConfiguration("Cube") {
                AutoFlushSendQueue = false
            };

#if UNITY_EDITOR
            if (lagSettings.enabled) {
                config.SimulatedLoss = lagSettings.simulatedLossPercent * 0.01f;
                config.SimulatedDuplicatesChance = lagSettings.duplicatesChancePercent * 0.01f;
                config.SimulatedMinimumLatency = lagSettings.minimumLatencyMs * 0.001f;
                config.SimulatedRandomLatency = lagSettings.additionalRandomLatencyMs * 0.001f;
            }
#endif

            client = new NetClient(config);
            client.Start();
        }

        public float GetRemoteTime(float time) {
            return (float)connection.GetRemoteTime(time);
        }

        public void Connect(string address, ushort port) {
            Debug.Log("[Client] <b>Connecting</b> to <i>" + address + ":" + port + "</i>");

            connection = client.Connect(address, port);
        }

        public void Connect(string address, ushort port, BitStream hailMessage) {
            Debug.Log("[Client] <b>Connecting</b> to <i>" + address + ":" + port + "</i>");

            var msg = client.CreateMessage(hailMessage.Length);
            msg.Write(hailMessage.Data, 0, hailMessage.Length);
            msg.LengthBits = hailMessage.LengthInBits;

            connection = client.Connect(address, port, msg);
        }

        public void Disconnect() {
            client.Disconnect("");
        }

        public void Update() {
            ReceiveMessages();
            client.FlushSendQueue();
            BitStreamPool.FrameReset();
        }

        public void Shutdown(uint blockDuration) {
            client.Shutdown("bye byte"); //#TODO message ?
        }

        public void Send(BitStream bs, PacketReliability reliablity, int sequenceChannel = 0) {
            var msg = client.CreateMessage(bs.Length);
            msg.Write(bs.Data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;
            client.SendMessage(msg, GetReliability(reliablity), sequenceChannel);
        }

        void ReceiveMessages() {
            while (true) {
                var msg = client.ReadMessage();
                if (msg == null)
                    break;

                switch (msg.MessageType) {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
#if CUBE_DEBUG_TRA
                    Debug.Log(msg.ReadString());
#endif
                        break;

                    case NetIncomingMessageType.WarningMessage:
                        Debug.LogWarning(msg.ReadString());
                        break;

                    case NetIncomingMessageType.ErrorMessage:
                        Debug.LogError(msg.ReadString());
                        NetworkError();
                        break;

                    case NetIncomingMessageType.Data: {
                            var bs = BitStream.CreateWithExistingBuffer(msg.Data, 0, msg.LengthBits);

                            var p = bs.Position;
                            var id = bs.ReadByte();
                            bs.Position = p;
                            Debug.Log(">> " + msg.DeliveryMethod + " " + id + " len=" + bs.Length + " " + bs);

                            ReceivedPacket(bs);
                            break;
                        }
                    case NetIncomingMessageType.StatusChanged: {
                            var status = (NetConnectionStatus)msg.ReadByte();
                            if (status == NetConnectionStatus.Connected) {
                                ConnectionRequestAccepted();
                            }
                            if (status == NetConnectionStatus.Disconnected) {
                                var reason = msg.ReadString();
                                Disconnected(reason);
                            }
                            break;
                        }
                    default: {
                            Debug.Log("[Client] Unhandled type: " + msg.MessageType);
                            break;
                        }
                }

                client.Recycle(msg);
            }
        }

        static NetDeliveryMethod GetReliability(PacketReliability reliability) {
            return reliability switch {
                PacketReliability.Unreliable => NetDeliveryMethod.Unreliable,
                PacketReliability.UnreliableSequenced => NetDeliveryMethod.UnreliableSequenced,
                PacketReliability.ReliableUnordered => NetDeliveryMethod.ReliableUnordered,
                PacketReliability.ReliableOrdered => NetDeliveryMethod.ReliableOrdered,
                PacketReliability.ReliableSequenced => NetDeliveryMethod.ReliableSequenced,
                _ => throw new ArgumentException("reliability"),
            };
        }
    }
}