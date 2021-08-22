using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Networking {
    public class CubeServer : ICubeServer {
        public IServerNetworkInterface NetworkInterface {
            get;
            internal set;
        }
        public ServerReactor Reactor {
            get;
            internal set;
        }
        public IServerReplicaManager ReplicaManager {
            get;
            internal set;
        }
        public List<Connection> connections {
            get;
            internal set;
        }

        public CubeServer(Transform replicaParentTransform, IServerNetworkInterface networkInterface, ServerReplicaManagerSettings replicaManagerSettings) {
            Assert.IsNotNull(replicaParentTransform);

            connections = new List<Connection>();

            NetworkInterface = networkInterface;
            networkInterface.NewConnectionEstablished += OnNewConnectionEstablished;
            networkInterface.DisconnectNotification += OnDisconnectNotification;

            Reactor = new ServerReactor(networkInterface);

            ReplicaManager = new ServerReplicaManager(this, replicaParentTransform, replicaManagerSettings);
        }

        public void Update() {
            ReplicaManager.Update();
            NetworkInterface.Update();
        }

        public void Shutdown() {
            NetworkInterface.Shutdown();

            ReplicaManager = null;
            Reactor = null;
            NetworkInterface = null;
        }

        void OnNewConnectionEstablished(Connection connection) {
            connections.Add(connection);
        }

        void OnDisconnectNotification(Connection connection) {
            connections.Remove(connection);
        }
    }
}