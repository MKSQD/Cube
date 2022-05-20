using System;
using System.Collections.Generic;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Cube {
    public class CubeServer : MonoBehaviour, ICubeServer {
        public event Action MapLoaded;

        public Transport.Transport Transport;
#if UNITY_EDITOR
        public Transport.Transport TransportInEditor;
#endif

        public IServerNetworkInterface NetworkInterface { get; private set; }
        public ServerReactor Reactor { get; private set; }
        public IServerReplicaManager ReplicaManager { get; private set; }
        public List<Connection> Connections { get; private set; }
        public Transform ReplicaParentTransform => transform;

        public ServerReplicaManagerSettings ReplicaManagerSettings;
        ServerReplicaManagerSettings ICubeServer.ReplicaManagerSettings => ReplicaManagerSettings;

        public bool IsLoadingMap { get; private set; }
        public string CurrentMapName { get; private set; }

        double _nextNetworkTick;

        AsyncOperationHandle<SceneInstance> _mapHandle;
        byte _loadMapGeneration;

        public void ReloadCurrentMap() => LoadMap(CurrentMapName);

        protected virtual void OnLeaveMap() { }

        /// <summary>
        /// Reset replication, instruct all clients to load the new scene, actually
        /// load the new scene on the server and finally create a new GameMode instance.
        /// </summary>
        /// <param name="name"></param>
        public void LoadMap(string name) {
            Debug.Log($"[Server] Loading map <i>{name}</i>...");

            Assert.IsTrue(name.Length > 0);

            if (IsLoadingMap)
                throw new Exception("Cant start loading, current loading");

            // Cleanup
            OnLeaveMap();

            IsLoadingMap = true;
            ++_loadMapGeneration;
            CurrentMapName = name;

            ReplicaManager.Reset();

            BroadcastLoadMap(name, _loadMapGeneration);

            // Disable ReplicaViews during level load
            foreach (var connection in Connections) {
                var replicaView = ReplicaManager.GetReplicaView(connection);
                if (replicaView != null) {
                    replicaView.IsLoadingLevel = true;
                }
            }

            // Unload old scene
            if (_mapHandle.IsValid()) {
                var op = Addressables.UnloadSceneAsync(_mapHandle);
                op.Completed += ctx => { LoadMapImpl(); };
                return;
            }

#if UNITY_EDITOR
            var loadedScene = SceneManager.GetSceneByName(name);
            if (loadedScene.isLoaded) {
                var op = SceneManager.UnloadSceneAsync(loadedScene);
                op.completed += ctx => { LoadMapImpl(); };
                return;
            }
#endif

            LoadMapImpl();
        }

        void BroadcastLoadMap(string sceneName, byte gen) {
            var bs = new BitWriter();
            bs.WriteByte((byte)MessageId.LoadScene);
            bs.WriteString(sceneName);
            bs.WriteByte(gen);

            NetworkInterface.BroadcastBitStream(bs, PacketReliability.ReliableSequenced, MessageChannel.SceneLoad);
        }

        void LoadMapImpl() {
            _mapHandle = Addressables.LoadSceneAsync(CurrentMapName, LoadSceneMode.Additive);
            _mapHandle.Completed += ctx => {
                Debug.Log("[Server] Map loaded");

                IsLoadingMap = false;
                MapLoaded?.Invoke();

                OnMapLoaded();
            };
        }

        protected virtual void OnMapLoaded() { }

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

            Reactor = new ServerReactor(NetworkInterface);
            ReplicaManager = new ServerReplicaManager(this);

            NetworkInterface.NewConnectionEstablished += OnNewConnectionEstablishedImpl;
            NetworkInterface.DisconnectNotification += OnDisconnectNotificationImpl;

            Reactor.AddHandler((byte)MessageId.LoadSceneDone, OnLoadSceneDone);
        }

        void OnLoadSceneDone(Connection connection, BitReader bs) {
            var generation = bs.ReadByte();
            if (generation != _loadMapGeneration)
                return;

            Debug.Log($"[Server] Client <i>{connection}</i> done loading scene (generation={generation})");

            //
            var replicaView = ReplicaManager.GetReplicaView(connection);
            if (replicaView != null) {
                replicaView.IsLoadingLevel = false;
                ReplicaManager.ForceReplicaViewRefresh(replicaView);
            }
        }

        protected virtual void Update() {
            ReplicaManager.Update();
            NetworkInterface.Update();

            if (Time.unscaledTimeAsDouble >= _nextNetworkTick) {
                _nextNetworkTick = Time.timeAsDouble + Constants.TickRate;
                TickImpl();
            }
        }

        void TickImpl() {
            ReplicaManager.Tick();
            Tick();
        }

        protected virtual void Tick() {
        }

        protected virtual void OnApplicationQuit() {
            NetworkInterface.Shutdown();

            ReplicaManager = null;
            Reactor = null;
            NetworkInterface = null;
        }

        protected virtual void OnNewConnectionEstablished(Connection connection, ReplicaView view) { }
        protected virtual void OnDisconnectNotification(Connection connection) { }

        void OnNewConnectionEstablishedImpl(Connection connection) {
            Debug.Log($"[Server] New connection <i>{connection}</i>");

            Connections.Add(connection);
            var replicaView = CreateReplicaView(connection);

            // Send load scene packet if we loaded one previously
            if (CurrentMapName != null) {
                var bs2 = new BitWriter();
                bs2.WriteByte((byte)MessageId.LoadScene);
                bs2.WriteString(CurrentMapName);
                bs2.WriteByte(_loadMapGeneration);

                NetworkInterface.Send(bs2, PacketReliability.ReliableSequenced, connection, MessageChannel.SceneLoad);
            }

            OnNewConnectionEstablished(connection, replicaView);
        }

        void OnDisconnectNotificationImpl(Connection connection) {
            Debug.Log("[Server] Lost connection: " + connection);

            Connections.Remove(connection);
            ReplicaManager.RemoveReplicaView(connection);

            OnDisconnectNotification(connection);
        }

        protected virtual ReplicaView CreateReplicaView(Connection connection) {
            var view = new GameObject("ReplicaView " + connection);
            view.transform.parent = transform;

            var rw = view.AddComponent<ReplicaView>();
            rw.Connection = connection;

            return rw;
        }
    }
}