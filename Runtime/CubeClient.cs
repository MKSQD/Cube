using Cube.Replication;
using Cube.Transport;
using Cube.Transport.Local;
using UnityEngine;

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
        }

        protected virtual void Start() {
            if (AutoConnectInEditor) {
                NetworkInterface.Connect("127.0.0.1");
            }
        }

        protected virtual void Update() {
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
            NetworkInterface.Shutdown(0);
        }
    }
}