using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cube.Transport {
    public delegate void ServerMessageHandler(Connection connection, BitReader bs);

    public class ServerReactor {
        readonly Dictionary<byte, ServerMessageHandler> handlers;

        public ServerReactor(IServerNetworkInterface networkInterface) {
            handlers = new Dictionary<byte, ServerMessageHandler>();

            networkInterface.ReceivedPacket += OnReceivedPacket;
        }

        void OnReceivedPacket(BitReader bs, Connection connection) {
            var messageId = bs.ReadByte();

            ServerMessageHandler handler;
            if (!handlers.TryGetValue(messageId, out handler)) {
                Debug.Log($"Received unknown packet {messageId}");
                return;
            }

            try {
                handler(connection, bs);
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        public void AddPacketHandler(byte id, ServerMessageHandler handler) {
            if (handlers.ContainsKey(id))
                throw new Exception("Message handler already set");

            handlers[id] = handler;
        }

        public void RemoveHandler(byte id) {
            handlers.Remove(id);
        }
    }
}

