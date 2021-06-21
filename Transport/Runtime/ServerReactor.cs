using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
    public delegate void ServerMessageHandler(Connection connection, BitStream bs);

    public class ServerReactor {
        readonly Dictionary<byte, List<ServerMessageHandler>> handlers;

        public ServerReactor(IServerNetworkInterface networkInterface) {
            handlers = new Dictionary<byte, List<ServerMessageHandler>>();

            networkInterface.ReceivedPacket += (bs, connection) => {
                var messageId = bs.ReadByte();

                List<ServerMessageHandler> packetHandlers;
                if (!handlers.TryGetValue(messageId, out packetHandlers) || packetHandlers.Count == 0) {
                    Debug.Log("Received unknown packet: " + messageId);
                    return;
                }

                foreach (var handler in packetHandlers) {
                    var pos = bs.Position;

                    try {
                        handler(connection, bs);
                    } catch (Exception e) {
                        Debug.LogException(e);
                    }
                    
                    bs.Position = pos;
                }
            };
        }

        public void AddMessageHandler(byte id, ServerMessageHandler handler) {
            if (!handlers.TryGetValue(id, out List<ServerMessageHandler> existingHandlers)) {
                existingHandlers = new List<ServerMessageHandler>();
                handlers.Add(id, existingHandlers);
            }
            existingHandlers.Add(handler);
        }

        public void RemoveMessageHandler(byte id, ServerMessageHandler handler) {
            if (!handlers.TryGetValue(id, out List<ServerMessageHandler> existingHandlers))
                return;

            existingHandlers.Remove(handler);
        }
    }
}

