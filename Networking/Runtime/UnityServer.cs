using Cube.Replication;
using Cube.Transport;
using System.Collections.Generic;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Networking {
    [AddComponentMenu("Cube.Networking/UnityServer")]
    public class UnityServer : IUnityServer {
#if SERVER
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

        List<Connection> _connections = new List<Connection>();
        public List<Connection> connections {
            get { return _connections; }
        }

        public UnityServer(ushort port, Transform serverTransform, ServerReplicaManagerSettings replicaManagerSettings) {
            networkInterface = new LidgrenServerNetworkInterface(port);

            reactor = new ServerReactor(networkInterface);
            reactor.AddHandler((byte)MessageId.NewConnectionEstablished, OnNewConnectionEstablished);
            reactor.AddHandler((byte)MessageId.DisconnectNotification, OnDisconnectNotification);
            
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
            _connections.Add(connection);
        }

        void OnDisconnectNotification(Connection connection, BitStream bs) {
            _connections.Remove(connection);
        }
#endif
    }
}