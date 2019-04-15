using Cube.Replication;
using Cube.Transport;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Networking {
    [Serializable]
    public class ConnectionEvent : UnityEvent<Connection> {
    }

    [AddComponentMenu("Cube/ServerGame")]
    public class ServerGame : NetworkBehaviour {
        public ushort port = 60000;

        public ServerReplicaManagerSettings replicaManagerSettings;

        public new UnityServer server;

        public ConnectionEvent onNewIncomingConnection;
        public ConnectionEvent onDisconnectionNotification;
        public UnityEvent onAllClientLoadedScene;

#if SERVER
        string _loadSceneName;
        byte _loadSceneGeneration;
        byte _loadScenePlayerAcks;

        public void LoadScene(string sceneName) {
            ++_loadSceneGeneration;
            _loadScenePlayerAcks = 0;
            _loadSceneName = sceneName;

            server.replicaManager.DestroyAllReplicas();

            var bs = new BitStream();
            bs.Write((byte)MessageId.LoadScene);
            bs.Write(sceneName); // #todo send scene idx instead
            bs.Write(_loadSceneGeneration);

            server.networkInterface.Broadcast(bs, PacketPriority.High, PacketReliability.ReliableSequenced);

            SceneManager.LoadScene(sceneName);
        }

        void Awake() {
            DontDestroyOnLoad(gameObject);

            var priorityManager = GetComponent<IReplicaPriorityManager>();
            if (priorityManager == null) {
                priorityManager = gameObject.AddComponent<DefaultReplicaPriorityManager>();
            }

            server = new UnityServer(port, transform, priorityManager, replicaManagerSettings);

            server.reactor.AddHandler((byte)MessageId.NewConnectionEstablished, OnNewIncomingConnection);
            server.reactor.AddHandler((byte)MessageId.DisconnectNotification, OnDisconnectionNotification);
            server.reactor.AddHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
        }

        void OnNewIncomingConnection(Connection connection, BitStream bs) {
            Debug.Log("New connection: " + connection);

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
            Debug.Log("Lost connection: " + connection);

            onDisconnectionNotification.Invoke(connection);
        }

        void OnLoadSceneDone(Connection connection, BitStream bs) {
            Debug.Log("On load scene done: " + connection);

            var generation = bs.ReadByte();
            if (generation != _loadSceneGeneration)
                return;

            ++_loadScenePlayerAcks;

            if (_loadScenePlayerAcks >= server.connections.Count) {
                onAllClientLoadedScene.Invoke();
            }
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