using Cube.Replication;
using Cube.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Networking {
    [AddComponentMenu("Cube/ClientGame")]
    public class ClientGame : NetworkBehaviour {
        public ClientSimulatedLagSettings lagSettings;

        public bool connectInEditor = true;
        public ushort portInEditor = 60000;

        public new UnityClient client;

#if CLIENT
        protected virtual void Awake() {
            DontDestroyOnLoad(gameObject);

            client = new UnityClient(transform, lagSettings);

//#if UNITY_EDITOR
            if (connectInEditor) {
                client.networkInterface.Connect("127.0.0.1", portInEditor);
                Debug.Log("Connecting to server...");
            }
//#endif

            client.reactor.AddHandler((byte)MessageId.ConnectionRequestAccepted, OnConnectionRequestAccepted);
            client.reactor.AddHandler((byte)MessageId.ConnectionRequestFailed, OnConnectionRequestFailed);
            client.reactor.AddHandler((byte)MessageId.LoadScene, OnLoadScene);
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

        protected virtual void OnLoadScene(BitStream bs) {
            var sceneName = bs.ReadString();
            var generation = bs.ReadByte();

            Debug.Log("Loading level: " + sceneName);

            client.replicaManager.DestroyAllReplicas();

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
                return; // See log for errors

            op.completed += _ => {
            var bs2 = new BitStream();
                bs2.Write((byte)MessageId.LoadSceneDone);
                bs2.Write(generation);

                client.networkInterface.Send(bs2, PacketPriority.High, PacketReliability.Reliable);
            };
        }
#endif
    }
}