using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;

namespace Cube.Networking {
    public class CubeServer : ICubeServer {
        public IServerNetworkInterface networkInterface {
            get;
            internal set;
        }
        public ServerReactor reactor {
            get;
            internal set;
        }
        public IServerReplicaManager replicaManager {
            get;
            internal set;
        }
        public IWorld world {
            get;
            internal set;
        }
        public List<Connection> connections {
            get;
            internal set;
        }

        public CubeServer(IWorld world, IServerNetworkInterface networkInterface, ServerReplicaManagerSettings replicaManagerSettings) {
            connections = new List<Connection>();
            this.world = world;

            this.networkInterface = networkInterface;
            networkInterface.NewConnectionEstablished += OnNewConnectionEstablished;
            networkInterface.DisconnectNotification += OnDisconnectNotification;

            reactor = new ServerReactor(networkInterface);
            replicaManager = new ServerReplicaManager(this, replicaManagerSettings);
        }

        public void Update() {
            replicaManager.Update();
            networkInterface.Update();
        }

        public void Shutdown() {
            networkInterface.Shutdown();

            replicaManager = null;
            reactor = null;
            networkInterface = null;
        }

        void OnNewConnectionEstablished(Connection connection) {
            connections.Add(connection);
        }

        void OnDisconnectNotification(Connection connection) {
            connections.Remove(connection);
        }
    }
}