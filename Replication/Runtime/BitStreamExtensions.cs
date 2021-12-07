﻿using Cube.Transport;
using UnityEngine;

namespace Cube.Replication {
    public static class BitStreamExtensions {
        public static void Write(this BitWriter bs, ReplicaId id) {
            bs.Write(id.Data);
        }

        public static void Read(this BitReader bs, ref ReplicaId val) {
            val = bs.ReadReplicaId();
        }

        public static ReplicaId ReadReplicaId(this BitReader bs) {
            var id = bs.ReadUShort();
            return ReplicaId.CreateFromExisting(id);
        }

        public static void Write(this BitWriter bs, NetworkObject networkObject) {
            WriteNetworkObject(bs, networkObject);
        }

        public static void WriteNetworkObject(this BitWriter bs, NetworkObject networkObject) {
            var max = NetworkObjectLookup.Instance.Entries.Length;
            var idx = networkObject != null ? networkObject.networkAssetId : -1;
            bs.WriteIntInRange(idx, -1, max);
        }

        public static T ReadNetworkObject<T>(this BitReader bs) where T : NetworkObject {
            var max = NetworkObjectLookup.Instance.Entries.Length;
            var id = bs.ReadIntInRange(-1, max);
            if (id == -1)
                return null;

            var networkObject = NetworkObjectLookup.Instance.CreateFromNetworkAssetId(id);
#if UNITY_EDITOR
            if (!(networkObject is T)) {
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