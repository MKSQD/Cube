#if SERVER

using UnityEngine;
using Lidgren.Network;
using UnityEngine.Assertions;
using System;

namespace Cube.Networking.Transport {
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

        public LidgrenServerNetworkInterface(ushort port) {
            bitStreamPool = new BitStreamPool();

            var config = new NetPeerConfiguration("Cube.Networking");
            config.Port = port; //#TODO move host and port to Interface (maybe as IServerNetworkInterfaceConfiguration)

            _server = new NetServer(config);
            _server.Start();
        }
        
        public void Shutdown() {
            _server.Shutdown("bye bye"); //#TODO message ???
        }
        
        public void Update() {
            bitStreamPool.FrameReset();
        }

        public Connection[] GetConnections() {
            var connections = new Connection[_server.ConnectionsCount];
            for (int i = 0; i < connections.Length; i++)
                connections[i] = new Connection((ulong)_server.Connections[0].RemoteUniqueIdentifier);

            return connections;
        }

        public void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity, Connection connection) {
            Assert.IsTrue(connection != Connection.Invalid);

            var msg = _server.CreateMessage(bs.Length);
            msg.Write(bs.data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;
            
            var netConnection = GetNetConnection(connection);
            _server.SendMessage(msg, netConnection, LidgrenToInternalReliability(reliablity), 0);
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
#if NETWORKING_LOG_INFO
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
                            //#TODO ugly
                            result = bitStreamPool.Create();
                            result.Write((byte)MessageId.NewConnectionEstablished);
                        }
                        if (status == NetConnectionStatus.Disconnected) {
                            //#TODO ugly
                            result = bitStreamPool.Create();
                            result.Write((byte)MessageId.DisconnectNotification);
                        }
                        break;
                    }
                case NetIncomingMessageType.Data:
                    result = BitStream.CreateWithExistingBuffer(msg.Data, msg.LengthBits);
                    break;

                default:
                    Debug.Log("Server - Unhandled type: " + msg.MessageType);
                    break;
            }

            _server.Recycle(msg);
            msg = null;

            return result;
        }

        NetDeliveryMethod LidgrenToInternalReliability(PacketReliability reliability) {
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

#endif