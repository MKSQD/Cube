using UnityEngine;
using Lidgren.Network;
using UnityEngine.Assertions;
using System;

namespace Cube.Transport {
    public class ConnectionNotFoundException : Exception {
    }

    public sealed class LidgrenServerNetworkInterface : IServerNetworkInterface {
        public Func<BitStream, ApprovalResult> ApproveConnection { get; set; }
        public Action<Connection> NewConnectionEstablished { get; set; }
        public Action NetworkError { get; set; }
        public Action<Connection> DisconnectNotification { get; set; }
        public Action<BitStream, Connection> ReceivedPacket { get; set; }

        public bool IsRunning => server.Status == NetPeerStatus.Running;


        NetServer server;

        public LidgrenServerNetworkInterface(string appIdentifier, ushort port, SimulatedLagSettings lagSettings) {
            var config = new NetPeerConfiguration(appIdentifier) {
                Port = port,
                AutoFlushSendQueue = false
            };
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

#if UNITY_EDITOR
            if (lagSettings.enabled) {
                config.SimulatedLoss = lagSettings.simulatedLossPercent * 0.01f;
                config.SimulatedDuplicatesChance = lagSettings.duplicatesChancePercent * 0.01f;
                config.SimulatedMinimumLatency = lagSettings.minimumLatencyMs * 0.001f;
                config.SimulatedRandomLatency = lagSettings.additionalRandomLatencyMs * 0.001f;
            }
#endif

#if !CUBE_DEBUG_TRA
            config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            config.DisableMessageType(NetIncomingMessageType.DebugMessage);
#endif

            server = new NetServer(config);
            server.Start();
        }

        public void Shutdown() {
            server.Shutdown("");
        }

        public void Update() {
            ReceiveMessages();
            server.FlushSendQueue();
            BitStreamPool.FrameReset();

#if UNITY_EDITOR
            TransportDebugger.CycleFrame();

            TransportDebugger.ReportStatistic($"Sent Bytes/s: {(int)(server.Statistics.SentBytes / Time.time)}");
            TransportDebugger.ReportStatistic($"Received Bytes/s {(int)(server.Statistics.ReceivedBytes / Time.time)}");

            TransportDebugger.ReportStatistic($"# Sent {server.Statistics.SentPackets}");
            TransportDebugger.ReportStatistic($"# Received {server.Statistics.ReceivedPackets}");
#endif
        }

        void ReceiveMessages() {
            while (true) {
                var msg = server.ReadMessage();
                if (msg == null)
                    break;

                var connection = Connection.Invalid;
                if (msg.SenderConnection != null) {
                    connection = new Connection((ulong)msg.SenderConnection.RemoteUniqueIdentifier);
                }

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

                    case NetIncomingMessageType.StatusChanged: {
                            var status = (NetConnectionStatus)msg.ReadByte();
                            if (status == NetConnectionStatus.Connected) {
                                NewConnectionEstablished(connection);
                            } else if (status == NetConnectionStatus.Disconnected) {
                                DisconnectNotification(connection);
                            }
                            break;
                        }
                    case NetIncomingMessageType.Data: {
                            var bs = BitStream.CreateWithExistingBuffer(msg.Data, 0, msg.LengthBits);
                            ReceivedPacket(bs, connection);
                            break;
                        }
                    case NetIncomingMessageType.ConnectionApproval: {
                            var bs = BitStream.CreateWithExistingBuffer(msg.Data, 0, msg.LengthBits);

                            try {
                                var approvalResult = ApproveConnection(bs);
                                if (approvalResult.Approved) {
                                    Debug.Log("[Server] Connection approved");
                                    msg.SenderConnection.Approve();
                                } else {
                                    Debug.Log($"[Server] Connection denied ({approvalResult.DenialReason})");
                                    msg.SenderConnection.Deny(approvalResult.DenialReason);
                                }
                            } catch (Exception e) {
                                Debug.LogException(e);
                                msg.SenderConnection.Deny("Approval Error");
                            }
                            break;
                        }

                    default:
                        Debug.Log("[Server] Unhandled type: " + msg.MessageType);
                        break;
                }

                server.Recycle(msg);
            }
        }

        public void Send(BitStream bs, PacketReliability reliablity, Connection connection, int sequenceChannel) {
            Debug.Log("<< " + reliablity + " " + bs.ReadByte() + " len=" + bs.Length + " " + bs);

            Assert.IsTrue(connection != Connection.Invalid);

            var msg = server.CreateMessage(bs.Length);
            msg.Write(bs.Data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;

            var netConnection = GetNetConnection(connection);
            server.SendMessage(msg, netConnection, GetReliability(reliablity), sequenceChannel);
        }

        public void BroadcastBitStream(BitStream bs, PacketReliability reliablity, int sequenceChannel) {
            Debug.Log("<< " + reliablity + " " + bs.ReadByte() + " len=" + bs.Length + " " + bs);

            if (server.Connections.Count == 0)
                return;

            var msg = server.CreateMessage(bs.Length);
            msg.Write(bs.Data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;

            server.SendMessage(msg, server.Connections, GetReliability(reliablity), sequenceChannel);
        }

        NetConnection GetNetConnection(Connection connection) {
            for (int i = 0; i < server.Connections.Count; ++i) {
                var con = server.Connections[i];
                if ((ulong)con.RemoteUniqueIdentifier == connection.id)
                    return con;
            }
            throw new ConnectionNotFoundException();
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
