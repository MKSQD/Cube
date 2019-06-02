using Cube.Replication;
using Cube.Transport;
using System;
using UnityEngine;
using UnityEngine.Events;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Networking {
    [Serializable]
    public class ConnectionEvent : UnityEvent<Connection> {
    }

    [AddComponentMenu("Cube/ServerGame")]
    public class ServerGame : MonoBehaviour {
        public ushort port = 60000;

        public ServerReplicaManagerSettings replicaManagerSettings;

        public UnityServer server;

        public ConnectionEvent onNewIncomingConnection;
        public ConnectionEvent onDisconnectionNotification;
        public UnityEvent onAllClientsLoadedScene;

        string _loadSceneName;
        byte _loadSceneGeneration;
        byte _loadScenePlayerAcks;
        bool _onAllClientsLoadedSceneTriggeredThisGeneration;

        public void LoadScene(string sceneName) {
            ++_loadSceneGeneration;
            _loadScenePlayerAcks = 0;
            _loadSceneName = sceneName;
            _onAllClientsLoadedSceneTriggeredThisGeneration = false;

            server.replicaManager.Reset();

            var bs = new BitStream();
            bs.Write((byte)MessageId.LoadScene);
            bs.Write(sceneName); // #todo send scene idx instead
            bs.Write(_loadSceneGeneration);

            server.networkInterface.Broadcast(bs, PacketPriority.High, PacketReliability.ReliableSequenced);

            // Disable Replicas during level load
            foreach (var connection in server.connections) {
                var replicaView = server.replicaManager.GetReplicaView(connection);
                if (replicaView == null)
                    continue;

                replicaView.isLoadingLevel = true;
            }

#if !UNITY_EDITOR
            Debug.Log("[Server] Loading level " + sceneName);
            SceneManager.LoadScene(sceneName);
#endif
        }

        void Awake() {
            DontDestroyOnLoad(gameObject);

            server = new UnityServer(port, transform, replicaManagerSettings);

            server.reactor.AddHandler((byte)MessageId.NewConnectionEstablished, OnNewIncomingConnection);
            server.reactor.AddHandler((byte)MessageId.DisconnectNotification, OnDisconnectionNotification);
            server.reactor.AddHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
        }

        void OnNewIncomingConnection(Connection connection, BitStream bs) {
            Debug.Log("[Server] New connection: " + connection);

            // Send load scene packet if we loaded one previously
            if (_loadSceneName != null) {
                var bs2 = new BitStream();
                bs2.Write((byte)MessageId.LoadScene);
                bs2.Write(_loadSceneName);
                bs2.Write(_loadSceneGeneration);

                server.networkInterface.Send(bs2, PacketPriority.High, PacketReliability.ReliableSequenced, connection);
            }

            onNewIncomingConnection.Invoke(connection);
        }

        void OnDisconnectionNotification(Connection connection, BitStream bs) {
            Debug.Log("[Server] Lost connection: " + connection);

            onDisconnectionNotification.Invoke(connection);

            OnNumReadyClientsChanged();
        }

        void OnLoadSceneDone(Connection connection, BitStream bs) {
            var generation = bs.ReadByte();
            if (generation != _loadSceneGeneration)
                return;

            Debug.Log("[Server] On load scene done: " + connection + " generation=" + generation);

            ++_loadScenePlayerAcks;

            OnNumReadyClientsChanged();

            //
            var replicaView = server.replicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.isLoadingLevel = false;
                server.replicaManager.ForceReplicaViewRefresh(replicaView);
            }
        }

        void OnNumReadyClientsChanged() {
            if (!_onAllClientsLoadedSceneTriggeredThisGeneration && _loadScenePlayerAcks >= server.connections.Count) {
                _onAllClientsLoadedSceneTriggeredThisGeneration = true;
                onAllClientsLoadedScene.Invoke();
            }
        }

        void Update() {
            server.Update();
        }

        void OnApplicationQuit() {
            server.Shutdown();
        }
    }
}