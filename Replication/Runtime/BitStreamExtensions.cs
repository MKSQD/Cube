using Cube.Transport;

namespace Cube.Replication {
    public static class BitStreamExtensions {
        public static void Read(this BitStream bs, ref ReplicaId val) {
            val = bs.ReadReplicaId();
        }

        public static ReplicaId ReadReplicaId(this BitStream bs) {
            var id = bs.ReadUShort();
            return ReplicaId.CreateFromExisting(id);
        }

        public static T ReadNetworkObject<T>(this BitStream bs) where T : NetworkObject {
            var id = bs.ReadString();
            if (id.Length == 0)
                return null;

            return (T)NetworkObjectLookup.instance.CreateFromNetworkAssetId(id);
        }

        public static void Write(this BitStream bs, ReplicaId id) {
            // #Optimize 2^32 impossible, reduce
            bs.Write(id.data);
        }
                
        public static void WriteNetworkObject(this BitStream bs, NetworkObject networkObject) {
            bs.Write(networkObject != null ? networkObject.networkAssetId : "");
        }
    }
}