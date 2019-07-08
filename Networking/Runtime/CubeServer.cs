using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Networking {
    public class CubeServer : ICubeServer {
        public IServerNetworkInterface networkInterface {
            get;
            internal set;
        }

        public IServerReactor reactor {
            get;
            internal set;
        }

        public IServerReplicaManager replicaManager {
            get;
            internal set;
        }

        public List<Connection> connections {
            get;
            internal set;
        }

        public CubeServer(ushort port, Transform serverTransform, ServerReplicaManagerSettings replicaManagerSettings) {
            connections = new List<Connection>();

            networkInterface = new LidgrenServerNetworkInterface(port);

            reactor = new ServerReactor(networkInterface);
            reactor.AddMessageHandler((byte)MessageId.NewConnectionEstablished, OnNewConnectionEstablished);
            reactor.AddMessageHandler((byte)MessageId.DisconnectNotification, OnDisconnectNotification);

            replicaManager = new ServerReplicaManager(this, serverTransform, replicaManagerSettings);
        }

        public void Update() {
            reactor.Update();
            replicaManager.Update();
            networkInterface.Update();
        }

        public void Shutdown() {
            networkInterface.Shutdown();
        }

        void OnNewConnectionEstablished(Connection connection, BitStream bs) {
            connections.Add(connection);
        }

        void OnDisconnectNotification(Connection connection, BitStream bs) {
            connections.Remove(connection);
        }
    }
}