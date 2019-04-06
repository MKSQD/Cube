using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube.Networking {
    [AddComponentMenu("Core/ServerGame")]
    public class ServerGame : NetworkBehaviour {
        public DefaultReplicaPriorityManager priorityManager;
        public ushort port = 60000;

#if SERVER
        
        public new UnityServer server;
        
        void Awake() {
            server = new UnityServer(port, transform, priorityManager);

            server.reactor.AddHandler((byte)MessageId.NewConnectionEstablished, OnNewIncomingConnection);
            server.reactor.AddHandler((byte)MessageId.DisconnectNotification, OnDisconnectionNotification);
        }
        
        protected virtual void OnNewIncomingConnection(Connection connection, Transport.BitStream bs) {
            Debug.Log("New connection: " + connection);
        }

        protected virtual void OnDisconnectionNotification(Connection connection, Transport.BitStream bs) {
            Debug.Log("Lost connection: " + connection);
        }
        
        void Update() {
            server.Update();
        }

        void OnApplicationQuit() {
            server.Shutdown();
        }
#endif
    }
}