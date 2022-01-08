using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube {
    public class CubeClient : MonoBehaviour, ICubeClient {
        public IClientNetworkInterface NetworkInterface { get; private set; }
        public ClientReactor Reactor { get; private set; }
        public IClientReplicaManager ReplicaManager { get; private set; }
        public Transform ReplicaParentTransform => transform;

        double _nextNetworkTick;

        protected virtual void Awake() {
            var transport = GetComponent<ITransport>();
            NetworkInterface = transport.CreateClient();

            Reactor = new ClientReactor(NetworkInterface);
            ReplicaManager = new ClientReplicaManager(this, NetworkPrefabLookup.Instance);
        }

        protected virtual void Update() {
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
            NetworkInterface.Shutdown(0);
        }
    }
}