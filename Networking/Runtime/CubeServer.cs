using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
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

        public IWorld world {
            get;
            internal set;
        }

        public List<Connection> connections {
            get;
            internal set;
        }

        public CubeServer(ushort port, IWorld world, SimulatedLagSettings lagSettings, ServerReplicaManagerSettings replicaManagerSettings) {
            connections = new List<Connection>();
            this.world = world;

            networkInterface = new LidgrenServerNetworkInterface(port, lagSettings);

            reactor = new ServerReactor(networkInterface);
            reactor.AddMessageHandler((byte)MessageId.NewConnectionEstablished, OnNewConnectionEstablished);
            reactor.AddMessageHandler((byte)MessageId.DisconnectNotification, OnDisconnectNotification);

            replicaManager = new ServerReplicaManager(this, replicaManagerSettings);
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