using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

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

        public void AddPacketHandler(byte id, ClientMessageHandler handler) {
            Assert.IsFalse(handlers.ContainsKey(id), "message handler already set");

            handlers[id] = handler;
        }

        public void RemoveHandler(byte id) {
            handlers.Remove(id);
        }
    }
}