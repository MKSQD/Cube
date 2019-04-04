using Cube.Networking;
using Cube.Networking.Replicas;
using Cube.Networking.Transport;

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
    }

    void Update() {
        client.Update();
    }

    void OnApplicationQuit() {
        client.Shutdown();
    }
#endif
}