using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Cube.Replication {
    [CreateAssetMenu(menuName = "Cube/NetworkPrefabs")]
    public class NetworkPrefabs : ScriptableObject {
        [Serializable]
        public struct PrefabPair {
            public GameObject Server, Client;
        }

        public GameObject[] Prefabs;
        public ushort[] Hashes;
        public PrefabPair[] Pairs;

        public GameObject GetClientPrefabForIndex(int prefabIdx) => Prefabs[prefabIdx];

        public int GetIndexForHash(ushort hash) {
            int minNum = 0;
            int maxNum = Hashes.Length - 1;

            while (minNum <= maxNum) {
                var mid = (minNum + maxNum) / 2;

                var midHash = Hashes[mid];
                if (hash == midHash) {
                    return mid;
                } else if (hash < midHash) {
                    maxNum = mid - 1;
                } else {
                    minNum = mid + 1;
                }
            }

            throw new Exception($"hash {hash} not found");
        }

#if UNITY_EDITOR
        public void Validate() {
            int numChanged = 0;

            var prefabs = new List<GameObject>(Prefabs.Length);
            var hashes = new List<ushort>(Prefabs.Length);
            foreach (var pair in Pairs) {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Server, out string guid, out long localId);

                var hash = NameToHash(guid);
                if (hashes.Contains(hash)) {
                    Debug.LogWarning($"Duplicated Replica Prefab Hash {guid} ({hash})");
                    continue;
                }

                hashes.Add(hash);

                var serverReplica = pair.Server.GetComponent<Replica>();
                if (serverReplica.PrefabHash != hash) {
                    serverReplica.PrefabHash = hash;
                    EditorUtility.SetDirty(pair.Server);
                    ++numChanged;
                }

                var clientReplica = pair.Client.GetComponent<Replica>();
                if (clientReplica.PrefabHash != hash) {
                    clientReplica.PrefabHash = hash;
                    EditorUtility.SetDirty(pair.Client);
                    ++numChanged;
                }

                // Fix copy&paste errors
                if (serverReplica.sceneIdx != 0) {
                    serverReplica.sceneIdx = 0;
                    EditorUtility.SetDirty(pair.Server);
                }
                if (clientReplica.sceneIdx != 0) {
                    clientReplica.sceneIdx = 0;
                    EditorUtility.SetDirty(clientReplica);
                }

                prefabs.Add(pair.Client);
            }

            var hashesArray = hashes.ToArray();
            var prefabsArray = prefabs.ToArray();
            Array.Sort(hashesArray, prefabsArray);

            var dirty = false;

            if (Prefabs == null || !Prefabs.SequenceEqual(prefabsArray)) {
                Prefabs = prefabsArray;
                dirty |= true;

            }
            if (Hashes == null || !Hashes.SequenceEqual(hashesArray)) {
                Hashes = hashesArray;
                dirty |= true;
            }

            if (dirty) {
                EditorUtility.SetDirty(this);
            }

            Debug.Log($"done (#changed={numChanged})");
        }

        static ushort NameToHash(string name) => (ushort)GetStableHashCode(name);

        static int GetStableHashCode(string str) {
            unchecked {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2) {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
#endif
    }
}