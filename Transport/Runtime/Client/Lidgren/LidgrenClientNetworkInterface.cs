using Lidgren.Network;
using UnityEngine;

namespace Cube.Transport {
#if CLIENT
    /// <summary>
    /// Client implementation with Lidgren.
    /// </summary>
    public class LidgrenClientNetworkInterface : IClientNetworkInterface {
        public BitStreamPool bitStreamPool {
            get;
            internal set;
        }

        NetClient _client;
        NetConnection _connection;
        
        public LidgrenClientNetworkInterface(ClientSimulatedLagSettings lagSettings) {
            bitStreamPool = new BitStreamPool();

            var config = new NetPeerConfiguration("Cube.Networking") {
                AutoFlushSendQueue = false
            };
            
#if UNITY_EDITOR
            if (lagSettings.enabled) {
                Debug.Log("Lag simulation enabled");
                
                config.SimulatedLoss = lagSettings.simulatedLoss;
                config.SimulatedDuplicatesChance = lagSettings.duplicatesChance;
                config.SimulatedMinimumLatency = lagSettings.minimumLatencySec;
                config.SimulatedRandomLatency = lagSettings.randomLatencySec;
            }
#endif

            _client = new NetClient(config);
            _client.Start();
        }
        
        public float GetRemoteTime(float time) {
            return (float)_connection.GetRemoteTime(time);
        }

        public void Connect(string address, ushort port) {
            _connection = _client.Connect(address, port);
        }

        public void Disconnect() {
            _client.Disconnect("");
        }

        public void Update() {
            bitStreamPool.FrameReset();
        }

        public void Shutdown(uint blockDuration) {
            _client.Shutdown("bye byte"); //#TODO message ?
        }

        public bool IsConnected() {
            return _connection != null && _connection.Status == NetConnectionStatus.Connected;
        }

        public unsafe void Send(BitStream bs, PacketPriority priority, PacketReliability reliablity) {
            var msg = _client.CreateMessage(bs.Length);
            msg.Write(bs.data, 0, bs.Length);
            msg.LengthBits = bs.LengthInBits;

            _client.SendMessage(msg, InternalReliabilityToLidgren(reliablity));
            _client.FlushSendQueue();
        }

        public unsafe BitStream Receive() {
            var msg = _client.ReadMessage();
            if (msg == null)
                return null;

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

                case NetIncomingMessageType.Data:
                    return BitStream.CreateWithExistingBuffer(msg.Data, msg.LengthBits);

                case NetIncomingMessageType.StatusChanged: {
                        var status = (NetConnectionStatus)msg.ReadByte();
                        if (status == NetConnectionStatus.Connected) {
                            var bs = bitStreamPool.Create();
                            bs.Write((byte)MessageId.ConnectionRequestAccepted);
                            return bs;
                        }
                        if (status == NetConnectionStatus.Disconnected) {
                            var bs = bitStreamPool.Create();
                            bs.Write((byte)MessageId.ConnectionRequestFailed);
                            return bs;
                        }
                        break;
                    }
                default: {
                        //Debug.Log("Client - Unhandled type: " + msg.MessageType);
                        break;
                    }
            }

            _client.Recycle(msg);
            return null;
        }

        NetDeliveryMethod InternalReliabilityToLidgren(PacketReliability reliability) {
            switch (reliability) {
                case PacketReliability.Unreliable: return NetDeliveryMethod.Unreliable;
                case PacketReliability.UnreliableSequenced: return NetDeliveryMethod.UnreliableSequenced;
                case PacketReliability.Reliable: return NetDeliveryMethod.ReliableUnordered;
                case PacketReliability.ReliableOrdered:  return NetDeliveryMethod.ReliableOrdered;
                case PacketReliability.ReliableSequenced: return NetDeliveryMethod.ReliableSequenced;
            }
            return NetDeliveryMethod.Unknown;
        }
    }
#endif
            }