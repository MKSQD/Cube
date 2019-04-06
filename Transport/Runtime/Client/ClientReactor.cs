using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
#if CLIENT
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor/Client</remarks>
    public class ClientReactor : IClientReactor {
        IClientNetworkInterface _networkInterface;
        public IClientNetworkInterface networkInterface {
            get { return _networkInterface; }
        }

        Dictionary<byte, List<ClientMessageHandler>> _handlers;

        public ClientReactor(IClientNetworkInterface networkInterface) {
            _networkInterface = networkInterface;
            _handlers = new Dictionary<byte, List<ClientMessageHandler>>();
        }

        public void AddHandler(byte id, ClientMessageHandler handler) {
            List<ClientMessageHandler> existingHandlers;
            if (!_handlers.TryGetValue(id, out existingHandlers)) {
                existingHandlers = new List<ClientMessageHandler>();
                _handlers.Add(id, existingHandlers);
            }

            existingHandlers.Add(handler);
        }

        public void Update() {
            while (true) {
                var bs = _networkInterface.Receive();
                if (bs == null)
                    break;

                var messageId = bs.ReadByte();

                List<ClientMessageHandler> handlers;
                if (!_handlers.TryGetValue(messageId, out handlers)) {
                    Debug.LogWarning("Received unknown packet " + messageId);
                    continue;
                }

                foreach (var handler in handlers) {
                    var pos = bs.Position;
                    handler(bs);
                    bs.Position = pos;
                }
            }
        }
    }
#endif
}