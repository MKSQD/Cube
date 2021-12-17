using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Cube.Replication;

namespace Cube.Networking.Tests {
    public class TestReplicaManager {

        [UnityTest]
        public IEnumerator TestInstantiateReplicaView() {
            var replicaViewPrefab = NetworkPrefabUtils.CreateReplica("ReplicaViewPrefab");
            replicaViewPrefab.AddComponent<ReplicaView>();

            var prefabLookup = NetworkPrefabUtils.CreateNetworkPrefabLookup(new List<GameObject> { replicaViewPrefab });

            var server = NetworkingUtils.InitServer(prefabLookup);
            var client = NetworkingUtils.InitClient(prefabLookup, server.server);
            
            var view = server.replicaManager.InstantiateReplica(replicaViewPrefab);

            var rw = view.GetComponent<ReplicaView>();
            rw.Connection = client.client.connection;

            server.replicaManager.AddReplicaView(rw);

            yield return NetworkingUtils.RunServerAndClientFor(server, client, 1f);

            Assert.IsTrue(client.gameObject.transform.childCount == 1);
            Assert.IsTrue(server.gameObject.transform.childCount == 1);

            Assert.IsNotNull(client.gameObject.transform.GetChild(0).GetComponent<Replica>());
            Assert.IsNotNull(client.gameObject.transform.GetChild(0).GetComponent<ReplicaView>());

            Assert.IsNotNull(server.gameObject.transform.GetChild(0).GetComponent<Replica>());
            Assert.IsNotNull(server.gameObject.transform.GetChild(0).GetComponent<ReplicaView>());
        }

        [UnityTest]
        public IEnumerator TestInstantiateReplica() {
            ServerObjects server;
            ClientObjects client;
            NetworkPrefabLookup lookup;
            NetworkingUtils.SetupNetworkScene(out server, out client, out lookup);
            
            var testPrefab = new GameObject("TestReplicaPrefab");
            testPrefab.AddComponent<Replica>();
            NetworkPrefabUtils.AddToNetworkPrefabLookup(lookup, testPrefab);
            
            server.replicaManager.InstantiateReplica(testPrefab);

            yield return NetworkingUtils.RunServerAndClientFor(server, client, 1f);

            Assert.IsTrue(client.gameObject.transform.childCount == 2);
            Assert.IsTrue(server.gameObject.transform.childCount == 2);
        }

    }
}
