using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube {
    public class CubeClient : MonoBehaviour, ICubeClient {
        public Transport.Transport Transport;
#if UNITY_EDITOR
        public Transport.Transport TransportInEditor;
#endif

        public IClientNetworkInterface NetworkInterface { get; private set; }
        public ClientReactor Reactor { get; private set; }
        public IClientReplicaManager ReplicaManager { get; private set; }

        double _nextNetworkTick;

        protected virtual void Awake() {
#if UNITY_EDITOR
            NetworkInterface = TransportInEditor.CreateClient();
#else
            NetworkInterface = Transport.CreateClient();
#endif

            Reactor = new ClientReactor(NetworkInterface);
            ReplicaManager = new ClientReplicaManager(this, transform, NetworkPrefabLookup.Instance);
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