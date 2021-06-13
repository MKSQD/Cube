using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
    public delegate void ClientMessageHandler(BitStream bs);

    public class ClientReactor {
        readonly Dictionary<byte, List<ClientMessageHandler>> handlers;

        public ClientReactor(IClientNetworkInterface networkInterface) {
            handlers = new Dictionary<byte, List<ClientMessageHandler>>();

            networkInterface.ReceivedPacket += OnReceivedPacket;
        }

        void OnReceivedPacket(BitStream bs) {
            var messageId = bs.ReadByte();

            List<ClientMessageHandler> handlers;
            if (!handlers.TryGetValue(messageId, out handlers) || handlers.Count == 0) {
                Debug.LogWarning("[Client] Received unknown packet " + messageId);
                return;
            }

            foreach (var handler in handlers) {
                var pos = bs.Position;

                try {
                    handler(bs);
                } catch (Exception e) {
                    Debug.LogException(e);
                }

                bs.Position = pos;
            }
        }

        public void AddHandler(byte id, ClientMessageHandler handler) {
            List<ClientMessageHandler> existingHandlers;
            if (!handlers.TryGetValue(id, out existingHandlers)) {
                existingHandlers = new List<ClientMessageHandler>();
                handlers.Add(id, existingHandlers);
            }

            existingHandlers.Add(handler);
        }

        public void RemoveHandler(byte id, ClientMessageHandler handler) {
            List<ClientMessageHandler> existingHandlers;
            if (!handlers.TryGetValue(id, out existingHandlers))
                return;

            existingHandlers.Remove(handler);
        }
    }
}