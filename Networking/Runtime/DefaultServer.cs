using Cube.Replication;
using Cube.Transport;
using UnityEngine;

namespace Cube.Networking {
    public class World : MonoBehaviour, IWorld { }

    [AddComponentMenu("Cube/DefaultServer")]
    public class DefaultServer : MonoBehaviour {
        public CubeServer Server;


        void Start() {
            var networkInterface = new LiteNetServerNetworkInterface(60000);
            var settings = new ServerReplicaManagerSettings();
            Server = new CubeServer(transform, networkInterface, settings);

            networkInterface.NewConnectionEstablished += conn => {
                Debug.Log($"[Server] New connection {conn}");

                var viewGO = new GameObject($"ReplicaView {conn}");
                var replicaView = viewGO.AddComponent<ReplicaView>();

                Server.ReplicaManager.AddReplicaView(replicaView);
            };
        }

        void Update() {
            Server.Update();
        }

        void OnDestroy() {
            Server.Shutdown();
        }
    }
}