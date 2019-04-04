using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cube.Networking.Transport;
using Cube.Networking.Transport.Tests;
using Cube.Networking.Replicas;

namespace Cube.Networking.Tests {
#if SERVER && CLIENT
    public struct ServerObjects {
        public LocalServerInterface server;
        public ServerReactor reactor;
        public GameObject gameObject;
        public ServerReplicaManager replicaManager;

        public IReplicaPriorityManager priorityManager;
    }

    public struct ClientObjects {
        public LocalClientInterface client;
        public ClientReactor reactor;
        public GameObject gameObject;
        public ClientReplicaManager replicaManager;
    }

    public class NetworkingUtils {
        public static ServerObjects InitServer(NetworkPrefabLookup prefabLookup) {
            var objects = new ServerObjects();

            objects.server = new LocalServerInterface();
            objects.reactor = new ServerReactor(objects.server);

            //#TODO TestReplicaPriorityManager (maybe remove MonoBehaviour from DefaultReplicaPriorityManager)
            var priorityManagerGO = new GameObject("priorityManager");
            objects.priorityManager = priorityManagerGO.AddComponent<DefaultReplicaPriorityManager>();

            objects.gameObject = new GameObject("Server");
            objects.replicaManager = new ServerReplicaManager(objects.reactor, objects.gameObject.transform, objects.priorityManager);

            return objects;
        }



        public static ClientObjects InitClient(NetworkPrefabLookup prefabLookup, LocalServerInterface server) {
            var objects = new ClientObjects();

            objects.client = new LocalClientInterface(server);
            objects.reactor = new ClientReactor(objects.client);
            objects.gameObject = new GameObject("Client");
            objects.replicaManager = new ClientReplicaManager(objects.reactor, prefabLookup, objects.gameObject.transform);

            return objects;
        }

        public static void SetupNetworkScene(out ServerObjects server, out ClientObjects client, out NetworkPrefabLookup prefabLookup) {
            var replicaViewPrefab = new GameObject("ReplicaViewPrefab");
            replicaViewPrefab.AddComponent<Replica>();
            replicaViewPrefab.AddComponent<ReplicaView>();

            prefabLookup = NetworkPrefabUtils.CreateNetworkPrefabLookup(new List<GameObject> { replicaViewPrefab });

            server = InitServer(prefabLookup);
            client = InitClient(prefabLookup, server.server);

            var view = server.replicaManager.InstantiateReplica(replicaViewPrefab);
            server.replicaManager.AddReplicaView(client.client.connection, view.GetComponent<ReplicaView>());
        }

        public static void UpdateServerAndClient(ServerObjects server, ClientObjects client) {
            server.reactor.Update();
            server.replicaManager.Update();

            client.reactor.Update();
            client.replicaManager.Update();
        }

        public static IEnumerator RunServerAndClientFor(ServerObjects server, ClientObjects client, float seconds) {
            yield return RunServerAndClientFor(server, client, seconds, 60);
        }

        public static IEnumerator RunServerAndClientFor(ServerObjects server, ClientObjects client, float seconds, int fps) {
            float timeLeft = seconds;
            float frameTime = seconds / (float)fps;

            while (timeLeft > 0f) {
                UpdateServerAndClient(server, client);
                Thread.Sleep((int)(1000f * frameTime));
                yield return null;
                timeLeft -= frameTime;
            }
        }
    }
#endif
}
