using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube.Networking {
    [AddComponentMenu("Cube/ServerGame")]
    public class ServerGame : NetworkBehaviour {
        public ushort port = 60000;

        public ServerReplicaManagerSettings replicaManagerSettings;

#if SERVER

        public new UnityServer server;

        void Awake() {
            var priorityManager = GetComponent<IReplicaPriorityManager>();
            if (priorityManager == null) {
                priorityManager = gameObject.AddComponent<DefaultReplicaPriorityManager>();
            }
            
            server = new UnityServer(port, transform, priorityManager, replicaManagerSettings);

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