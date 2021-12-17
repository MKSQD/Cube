using Cube.Transport;
using UnityEngine;

namespace Cube {
    [AddComponentMenu("Cube/DefaultClient")]
    public class DefaultClient : MonoBehaviour {
        CubeClient client;

        void Start() {
            var world = new GameObject("Client World");
            var worldC = world.AddComponent<World>();

            var transport = GetComponent<ITransport>();

            var networkInterface = transport.CreateClient();
            networkInterface.ConnectionRequestAccepted += () => Debug.Log($"[Client] Connection request accepted");

            client = new CubeClient(worldC, networkInterface);
            client.NetworkInterface.Connect("127.0.0.1", 60000);
        }

        void Update() {
            client.Update();
        }

        void OnDestroy() {
            client.Shutdown();
        }
    }
}