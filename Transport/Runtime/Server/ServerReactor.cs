using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
#if SERVER
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor/Server</remarks>
    public class ServerReactor : IServerReactor {
        public IServerNetworkInterface networkInterface {
            get;
            internal set;
        }

        Dictionary<byte, List<ServerMessageHandler>> _handlers;

        public ServerReactor(IServerNetworkInterface networkInterface) {
            this.networkInterface = networkInterface;
            _handlers = new Dictionary<byte, List<ServerMessageHandler>>();
        }

        public void AddHandler(byte id, ServerMessageHandler handler) {
            List<ServerMessageHandler> existingHandlers;
            if (!_handlers.TryGetValue(id, out existingHandlers)) {
                existingHandlers = new List<ServerMessageHandler>();
                _handlers.Add(id, existingHandlers);
            }
            existingHandlers.Add(handler);
        }

        public void RemoveHandler(byte id, ServerMessageHandler handler) {
            List<ServerMessageHandler> existingHandlers;
            if (!_handlers.TryGetValue(id, out existingHandlers))
                return;

            existingHandlers.Remove(handler);
        }

        public void Update() {
            while (true) {
                Connection connection;
                var bs = networkInterface.Receive(out connection);
                if (bs == null)
                    break;
                //#TODO force break after x packets ?

                var messageId = bs.ReadByte();

                List<ServerMessageHandler> handlers;
                if (_handlers.TryGetValue(messageId, out handlers)) {
                    foreach (var handler in handlers) {
                        var pos = bs.Position;
                        handler(connection, bs);
                        bs.Position = pos;
                    }
                } else {
                    Debug.Log("Received unknown packet: " + messageId);
                }
            }
        }
    }
#endif
}

