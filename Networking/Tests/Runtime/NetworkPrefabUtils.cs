using System.Collections.Generic;
using UnityEngine;
using Cube.Networking.Replicas;

namespace Cube.Networking.Tests {
    public class NetworkPrefabUtils {
      
        public static NetworkPrefabLookup CreateNetworkPrefabLookup(List<GameObject> gameObjects) {
            //#TODO
            return null;

//             var prefabLookup = ScriptableObject.CreateInstance<NetworkPrefabLookup>();
//             prefabLookup.prefabs = new NetworkPrefabLookup.NetworkPrefab[gameObjects.Count];
// 
//             for (int i = 0; i < gameObjects.Count; i++) {
//                 if (gameObjects[i].GetComponent<Replica>() == null)
//                     throw new Exception("GameObject requires Replica");
// 
//                 prefabLookup.prefabs[i] = new NetworkPrefabLookup.NetworkPrefab {
//                     client = gameObjects[i],
//                     server = gameObjects[i]
//                 };
//             }
// 
//             return prefabLookup;
        }

        public static void AddToNetworkPrefabLookup(NetworkPrefabLookup lookup, GameObject gameObject) {
            AddToNetworkPrefabLookup(lookup, new List<GameObject>() { gameObject });
        }

        public static void AddToNetworkPrefabLookup(NetworkPrefabLookup lookup, List<GameObject> gameObjects) {
            //#TODO
//             var newPrefabs = new List<NetworkPrefabLookup.NetworkPrefab>();
//             newPrefabs.AddRange(lookup.prefabs);
// 
//             foreach (var obj in gameObjects) {
//                 if (obj.GetComponent<Replica>() == null)
//                     throw new Exception("GameObject requires Replica");
// 
//                 newPrefabs.Add(new NetworkPrefabLookup.NetworkPrefab {
//                     client = obj,
//                     server = obj
//                 });
//             }
// 
//             lookup.prefabs = newPrefabs.ToArray();
        }

        public static GameObject CreateReplica(string name) {
            var go = new GameObject(name);
            go.AddComponent<Replica>();
            return go;
        }
    }
}
