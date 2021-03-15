using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
    public delegate void ServerMessageHandler(Connection connection, BitStream bs);

    public class ServerReactor {
        public IServerNetworkInterface networkInterface {
            get;
            internal set;
        }

        Dictionary<byte, List<ServerMessageHandler>> _handlers;

        public ServerReactor(IServerNetworkInterface networkInterface) {
            _handlers = new Dictionary<byte, List<ServerMessageHandler>>();

            this.networkInterface = networkInterface;

            networkInterface.ReceivedPacket += (bs, connection) => {
                var messageId = bs.ReadByte();

                List<ServerMessageHandler> handlers;
                if (!_handlers.TryGetValue(messageId, out handlers) || handlers.Count == 0) {
                    Debug.Log("Received unknown packet: " + messageId);
                    return;
                }

                foreach (var handler in handlers) {
                    var pos = bs.Position;

                    try {
                        handler(connection, bs);
                    }
                    catch (Exception e) {
                        Debug.LogException(e);
                    }

                    bs.Position = pos;
                }
            };
        }

        public void AddMessageHandler(byte id, ServerMessageHandler handler) {
            if (!_handlers.TryGetValue(id, out List<ServerMessageHandler> existingHandlers)) {
                existingHandlers = new List<ServerMessageHandler>();
                _handlers.Add(id, existingHandlers);
            }
            existingHandlers.Add(handler);
        }

        public void RemoveMessageHandler(byte id, ServerMessageHandler handler) {
            if (!_handlers.TryGetValue(id, out List<ServerMessageHandler> existingHandlers))
                return;

            existingHandlers.Remove(handler);
        }
    }
}

