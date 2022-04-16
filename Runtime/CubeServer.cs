using System.Collections.Generic;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube {
    public class CubeServer : MonoBehaviour, ICubeServer {
        public Transport.Transport Transport;
#if UNITY_EDITOR
        public Transport.Transport TransportInEditor;
#endif
        [SerializeField]
        NetworkPrefabLookup _prefabLookup;
        public NetworkPrefabLookup PrefabLookup => _prefabLookup;
        [SerializeField]
        NetworkObjectLookup _objectLookup;
        public NetworkObjectLookup ObjectLookup => _objectLookup;

        public IServerNetworkInterface NetworkInterface { get; private set; }
        public ServerReactor Reactor { get; private set; }
        public IServerReplicaManager ReplicaManager { get; private set; }
        public List<Connection> Connections { get; private set; }
        public Transform ReplicaParentTransform => transform;

        public ServerReplicaManagerSettings ReplicaManagerSettings;
        ServerReplicaManagerSettings ICubeServer.ReplicaManagerSettings => ReplicaManagerSettings;

        double _nextNetworkTick;

        protected virtual void Awake() {
            Connections = new List<Connection>();

#if UNITY_EDITOR
            if (TransportInEditor == null || Transport == null) {
                Debug.LogError("Either TransportInEditor or Transport are not set to anything."
                    + " Create a new Transport of your choice (Project > Create > Cube > ...)"
                    + " and set that on both the client and the server.", gameObject);
            }

            NetworkInterface = TransportInEditor.CreateServer();
#else
            NetworkInterface = Transport.CreateServer();
#endif

            NetworkInterface.NewConnectionEstablished += OnNewConnectionEstablished;
            NetworkInterface.DisconnectNotification += OnDisconnectNotification;

            Reactor = new ServerReactor(NetworkInterface);
            ReplicaManager = new ServerReplicaManager(this);
        }

        protected virtual void Update() {
            ReplicaManager.Update();
            NetworkInterface.Update();

            if (Time.unscaledTimeAsDouble >= _nextNetworkTick) {
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
            Connections.Add(connection);
        }

        void OnDisconnectNotification(Connection connection) {
            Connections.Remove(connection);
        }
    }
}