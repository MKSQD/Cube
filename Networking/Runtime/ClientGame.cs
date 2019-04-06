using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Networking {
    [AddComponentMenu("Core/ClientGame")]
    public class ClientGame : NetworkBehaviour {
        public ClientSimulatedLagSettings lagSettings;

        public bool connectInEditor = true;
        public ushort portInEditor = 60000;

#if CLIENT
        public new UnityClient client;

        void Awake() {
            client = new UnityClient(transform, lagSettings);

#if UNITY_EDITOR
            if (connectInEditor) {
                client.networkInterface.Connect("127.0.0.1", portInEditor);
            }
#endif

            client.reactor.AddHandler((byte)MessageId.ConnectionRequestAccepted, OnConnectionRequestAccepted);
            client.reactor.AddHandler((byte)MessageId.ConnectionRequestFailed, OnConnectionRequestFailed);
        }

        void Update() {
            client.Update();
        }

        void OnApplicationQuit() {
            client.Shutdown();
        }

        protected virtual void OnConnectionRequestAccepted(BitStream bs) {
            Debug.Log("Connection request to server accepted");
        }

        protected virtual void OnConnectionRequestFailed(BitStream bs) {
            Debug.Log("Connection request to server failed");
        }
#endif
    }
}