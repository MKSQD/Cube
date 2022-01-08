using System.Collections.Generic;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube {
    public class CubeServer : MonoBehaviour, ICubeServer {
        public IServerNetworkInterface NetworkInterface { get; private set; }
        public ServerReactor Reactor { get; private set; }
        public IServerReplicaManager ReplicaManager { get; private set; }
        public List<Connection> connections { get; private set; }
        public Transform ReplicaParentTransform => transform;

        public ushort Port = 60000;
        public int NumMaxClients = 30;
        public SimulatedLagSettings LagSettings;
        public ServerReplicaManagerSettings ReplicaManagerSettings;
        ServerReplicaManagerSettings ICubeServer.ReplicaManagerSettings => ReplicaManagerSettings;

        double _nextNetworkTick;

        protected virtual void Awake() {
            connections = new List<Connection>();

            var transport = GetComponent<ITransport>();

            NetworkInterface = transport.CreateServer(NumMaxClients, LagSettings);
            NetworkInterface.Start(Port);

            NetworkInterface.NewConnectionEstablished += OnNewConnectionEstablished;
            NetworkInterface.DisconnectNotification += OnDisconnectNotification;

            Reactor = new ServerReactor(NetworkInterface);

            ReplicaManager = new ServerReplicaManager(this);
        }

        protected virtual void Update() {
            ReplicaManager.Update();
            NetworkInterface.Update();

            if (Time.timeAsDouble >= _nextNetworkTick) {
                _nextNetworkTick = Time.timeAsDouble + Constants.TickRate;
                Tick();
            }
        }

        protected virtual void Tick() {
            ReplicaManager.Tick();
        }

        protected virtual void OnApplicationQuit() {
            NetworkInterface.Shutdown();

            ReplicaManager = null;
            Reactor = null;
            NetworkInterface = null;
        }

        protected virtual void TickImpl() { }

        void OnNewConnectionEstablished(Connection connection) {
            connections.Add(connection);
        }

        void OnDisconnectNotification(Connection connection) {
            connections.Remove(connection);
        }
    }
}