using UnityEngine;
using Lidgren.Network;
using UnityEngine.Assertions;
using System;

namespace Cube.Transport {
    public class ConnectionNotFoundException : Exception {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor/Server</remarks>
    public sealed class LidgrenServerNetworkInterface : IServerNetworkInterface {
        public BitStreamPool bitStreamPool {
            get;
            internal set;
        }

        public bool isRunning {
            get { return _server.Status == NetPeerStatus.Running; }
        }

        NetServer _server;

        public LidgrenServerNetworkInterface(ushort port, SimulatedLagSettings lagSettings) {
            bitStreamPool = new BitStreamPool();

            var config = new NetPeerConfiguration("Cube") {
                Port = port,
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

#if !CUBE_DEBUG_TRA
            config.DisableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            config.DisableMessageType(NetIncomingMessageType.DebugMessage);
#endif

            _server = new NetServer(config);
            _server.Start();
        }

        public void Shutdown() {
            _server.Shutdown("");
        }

        public void Update() {
            _server.FlushSendQueue();
            bitStreamPool.FrameReset();
        }

        public Connection[] GetConnections() {
            var connections = new Connection[_server.ConnectionsCount];
            for (int i = 0; i < connections.Length; ++i) {
                connections[i] = new Connection((ulong)_server.Connections[0].RemoteUniqueIdentifier);
            }

            return connections;
        }

        public void SendBitStream(BitStream bs, PacketPriority priority, PacketReliability reliablity, Connection connection, int sequenceChannel) {
            Assert.IsTrue(connection != Connection.Invalid);

            var msg = _server.CreateMessage(bs.Length);
            msg.Write(bs.data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;

            var netConnection = GetNetConnection(connection);
            _server.SendMessage(msg, netConnection, LidgrenToInternalReliability(reliablity), sequenceChannel);
        }

        public void BroadcastBitStream(BitStream bs, PacketPriority priority, PacketReliability reliablity, int sequenceChannel) {
            if (_server.Connections.Count == 0)
                return;

            var msg = _server.CreateMessage(bs.Length);
            msg.Write(bs.data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;

            _server.SendMessage(msg, _server.Connections, LidgrenToInternalReliability(reliablity), sequenceChannel);
        }

        NetConnection GetNetConnection(Connection connection) {
            for (int i = 0; i < _server.Connections.Count; ++i) {
                var con = _server.Connections[i];
                if ((ulong)con.RemoteUniqueIdentifier == connection.id)
                    return con;
            }
            throw new ConnectionNotFoundException();
        }

        public BitStream Receive(out Connection connection) {
            connection = Connection.Invalid;

            var msg = _server.ReadMessage();
            if (msg == null)
                return null;

            if (msg.SenderConnection != null) {
                connection = new Connection((ulong)msg.SenderConnection.RemoteUniqueIdentifier);
            }

            BitStream result = null;

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

                case NetIncomingMessageType.StatusChanged: {
                        var status = (NetConnectionStatus)msg.ReadByte();
                        msg.ReadString();

                        if (status == NetConnectionStatus.Connected) {
                            result = bitStreamPool.Create();
                            result.Write((byte)MessageId.NewConnectionEstablished);
                        }
                        if (status == NetConnectionStatus.Disconnected) {
                            result = bitStreamPool.Create();
                            result.Write((byte)MessageId.DisconnectNotification);
                        }
                        break;
                    }
                case NetIncomingMessageType.Data:
                    result = BitStream.CreateWithExistingBuffer(msg.Data, msg.LengthBits);
                    break;

                default:
                    Debug.Log("[Server] Unhandled type: " + msg.MessageType);
                    break;
            }

            _server.Recycle(msg);

            return result;
        }

        NetDeliveryMethod LidgrenToInternalReliability(PacketReliability reliability) {
            switch (reliability) {
                case PacketReliability.Unreliable: return NetDeliveryMethod.Unreliable;
                case PacketReliability.UnreliableSequenced: return NetDeliveryMethod.UnreliableSequenced;
                case PacketReliability.Reliable: return NetDeliveryMethod.ReliableUnordered;
                case PacketReliability.ReliableOrdered: return NetDeliveryMethod.ReliableOrdered;
                case PacketReliability.ReliableSequenced: return NetDeliveryMethod.ReliableSequenced;
                default: return NetDeliveryMethod.Unknown;
            }
        }
    }
}
