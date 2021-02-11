using Lidgren.Network;
using System;
using UnityEngine;

namespace Cube.Transport {
    /// <summary>
    /// Client implementation with Lidgren.
    /// </summary>
    public class LidgrenClientNetworkInterface : IClientNetworkInterface {
        public Action ConnectionRequestAccepted { get; set; }
        public Action<string> Disconnected { get; set; }

        public BitStreamPool bitStreamPool {
            get;
            internal set;
        }

        NetClient client;
        NetConnection connection;

        public LidgrenClientNetworkInterface(SimulatedLagSettings lagSettings) {
            bitStreamPool = new BitStreamPool();

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
            msg.Write(hailMessage.data, 0, hailMessage.Length);
            msg.LengthBits = hailMessage.LengthInBits;

            connection = client.Connect(address, port, msg);
        }

        public void Disconnect() {
            client.Disconnect("");
        }

        public void Update() {
            client.FlushSendQueue();
            bitStreamPool.FrameReset();
        }

        public void Shutdown(uint blockDuration) {
            client.Shutdown("bye byte"); //#TODO message ?
        }

        public bool IsConnected() {
            return connection != null && connection.Status == NetConnectionStatus.Connected;
        }

        public unsafe void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity) {
            var msg = client.CreateMessage(bs.Length);
            msg.Write(bs.data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;

            client.SendMessage(msg, InternalReliabilityToLidgren(reliablity));
        }

        public unsafe BitStream Receive() {
            var msg = client.ReadMessage();
            if (msg == null)
                return null;

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
                    break;

                case NetIncomingMessageType.Data:
                    return BitStream.CreateWithExistingBuffer(msg.Data, msg.LengthBits);

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
                        Debug.Log("Client - Unhandled type: " + msg.MessageType);
                        break;
                    }
            }

            client.Recycle(msg);
            return null;
        }

        NetDeliveryMethod InternalReliabilityToLidgren(PacketReliability reliability) {
            switch (reliability) {
                case PacketReliability.Unreliable: return NetDeliveryMethod.Unreliable;
                case PacketReliability.UnreliableSequenced: return NetDeliveryMethod.UnreliableSequenced;
                case PacketReliability.Reliable: return NetDeliveryMethod.ReliableUnordered;
                case PacketReliability.ReliableOrdered: return NetDeliveryMethod.ReliableOrdered;
                case PacketReliability.ReliableSequenced: return NetDeliveryMethod.ReliableSequenced;
            }
            return NetDeliveryMethod.Unknown;
        }
    }
}