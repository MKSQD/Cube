using Cube.Transport;
using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Replication {
    public static class BitStreamExtensions {
        public static void WriteReplicaId(this IBitWriter bs, Replica replica) {
            Assert.IsNotNull(replica);
            bs.WriteUShort(replica.Id.Data);
        }
        public static void WriteReplicaId(this IBitWriter bs, ReplicaId id) => bs.WriteUShort(id.Data);

        public static void Read(this BitReader bs, ref ReplicaId val) => val = bs.ReadReplicaId();

        public static ReplicaId ReadReplicaId(this BitReader bs) {
            var id = bs.ReadUShort();
            return ReplicaId.CreateFromExisting(id);
        }

        public static void WriteNetworkObject(this IBitWriter bs, NetworkObject networkObject) {
            if (networkObject != null) {
                Assert.IsTrue(networkObject == NetworkObjectLookup.Instance.CreateFromNetworkAssetId(networkObject.NetworkAssetId));
            }

            var max = NetworkObjectLookup.Instance.Entries.Length;
            var idx = networkObject != null ? networkObject.NetworkAssetId : -1;
            bs.WriteIntInRange(idx, -1, max);
        }

        public static T ReadNetworkObject<T>(this BitReader bs) where T : NetworkObject {
            var max = NetworkObjectLookup.Instance.Entries.Length;
            var id = bs.ReadIntInRange(-1, max);
            if (id == -1)
                return null;

            var networkObject = NetworkObjectLookup.Instance.CreateFromNetworkAssetId(id);
#if UNITY_EDITOR
            if (networkObject is not T) {
                Debug.Log($"Merde; Tried to read {typeof(T).Name}, but NO was {networkObject.GetType().Name}");
                return null;
            }
#endif
            return (T)networkObject;
        }

        public static void Read<T>(this BitReader bs, ref T value) where T : NetworkObject {
            value = bs.ReadNetworkObject<T>();
        }
    }
}