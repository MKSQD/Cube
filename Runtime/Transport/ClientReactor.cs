using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
    public delegate void ClientMessageHandler(BitReader bs);

    public class ClientReactor {
        readonly Dictionary<byte, ClientMessageHandler> handlers;

        public ClientReactor(IClientNetworkInterface networkInterface) {
            handlers = new Dictionary<byte, ClientMessageHandler>();

            networkInterface.ReceivedPacket += OnReceivedPacket;
        }

        void OnReceivedPacket(BitReader bs) {
            var messageId = bs.ReadByte();

            ClientMessageHandler packetHandler;
            if (!handlers.TryGetValue(messageId, out packetHandler)) {
                Debug.LogWarning("[Client] Received unknown packet " + messageId);
                return;
            }

            try {
                packetHandler(bs);
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        public void AddHandler(byte id, ClientMessageHandler handler) {
            if (handlers.ContainsKey(id))
                throw new Exception("Message handler already set");

            handlers[id] = handler;
        }

        public void RemoveHandler(byte id) {
            handlers.Remove(id);
        }
    }
}