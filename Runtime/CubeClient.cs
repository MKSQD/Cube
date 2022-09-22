using System.Collections;
using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Cube {
    public class CubeClient : MonoBehaviour, ICubeClient {
        public Transport.Transport Transport;
#if UNITY_EDITOR
        public Transport.Transport TransportInEditor;
#endif

        public IClientNetworkInterface NetworkInterface { get; private set; }
        public ClientReactor Reactor { get; private set; }
        public IClientReplicaManager ReplicaManager { get; private set; }

        public bool AutoConnectInEditor = true;

        double _nextNetworkTick;

        AsyncOperationHandle<SceneInstance> _sceneHandle;
        byte _currentLoadedSceneGeneration;

        protected virtual void Awake() {
#if UNITY_EDITOR
            if (TransportInEditor == null || Transport == null) {
                Debug.LogError("Either TransportInEditor or Transport are not set to anything."
                    + " This will break networking in the build standalone player."
                    + " Defaulting to local transport...", gameObject);
            }

            NetworkInterface = TransportInEditor.CreateClient();
#else
            NetworkInterface = Transport.CreateClient();
#endif

            Reactor = new ClientReactor(NetworkInterface);
            ReplicaManager = new ClientReplicaManager(this, transform);

            Reactor.AddPacketHandler((byte)MessageId.LoadScene, OnLoadScene);

            NetworkInterface.Disconnected += OnDisconnectedImpl;
        }

        protected virtual void Start() {
#if UNITY_EDITOR
            if (AutoConnectInEditor) {
                NetworkInterface.Connect("127.0.0.1");
            }
#endif
        }

        protected virtual void Update() {
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

        protected virtual void Tick() { }

        protected virtual void OnApplicationQuit() {
            ReplicaManager.Shutdown();
            NetworkInterface.Shutdown(0);
        }

        protected virtual void OnDisconnected() { }

        void OnDisconnectedImpl(string reason) {
            Debug.Log($"[Client] <b>Disconnected</b> ({reason})");

            OnDisconnected();

            ReplicaManager.Reset();

            if (_sceneHandle.IsValid()) {
                Addressables.UnloadSceneAsync(_sceneHandle);
            }
        }

        void OnLoadScene(BitReader bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            if (_currentLoadedSceneGeneration != generation) {
                _currentLoadedSceneGeneration = generation;

                StartCoroutine(LoadMap(sceneName));
            }
        }

        protected virtual void OnStartedLoadingMap(string mapName) { }
        protected virtual void OnEndedLoadingMap() { }

        IEnumerator LoadMap(string mapName) {
            Debug.Log($"[Client] <b>Loading map</b> <i>{mapName}</i>");

            OnStartedLoadingMap(mapName);

            ReplicaManager.Reset();

            if (_sceneHandle.IsValid())
                yield return Addressables.UnloadSceneAsync(_sceneHandle);

#if UNITY_EDITOR
            // Assume server loaded map already
            var scene = SceneManager.GetSceneByName(mapName);
            Assert.IsTrue(scene.IsValid());
#else
            _sceneHandle = Addressables.LoadSceneAsync(mapName, LoadSceneMode.Additive);
            yield return _sceneHandle;
            var scene = _sceneHandle.Result.Scene;

            
#endif

            ReplicaManager.ProcessSceneReplicasInScene(scene);

            SendLoadSceneDone();
            OnEndedLoadingMap();

#if !UNITY_EDITOR
           EventHub<MapLoaded>.Emit(new());
#endif
        }

        void SendLoadSceneDone() {
            var bs = new BitWriter(1);
            bs.WriteByte((byte)MessageId.LoadSceneDone);
            bs.WriteByte(_currentLoadedSceneGeneration);

            NetworkInterface.Send(bs, PacketReliability.ReliableUnordered, MessageChannel.SceneLoad);
        }
    }
}