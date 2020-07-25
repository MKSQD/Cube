using Cube.Transport;

namespace Cube.Replication {
    public static class BitStreamExtensions {
        public static void Write(this BitStream bs, ReplicaId id) {
            bs.Write(id.data);
        }

        public static void Read(this BitStream bs, ref ReplicaId val) {
            val = bs.ReadReplicaId();
        }

        public static ReplicaId ReadReplicaId(this BitStream bs) {
            var id = bs.ReadUShort();
            return ReplicaId.CreateFromExisting(id);
        }


        public static void WriteNetworkObject(this BitStream bs, NetworkObject networkObject) {
            bs.Write(networkObject != null ? networkObject.networkAssetId : "");

            // #todo this is stupid (32 bytes instead of 16 + could use ranged int because we know how many NetworkObjects (or maybe Ts?) there are)
//             bs.Write(networkObject == null);
//             if (networkObject != null) {
//                 for (int i = 0; i < 32; ++i) {
//                     var b = (byte)networkObject.networkAssetId[i];
//                     bs.Write(b);
//                 }
//             }
        }

        public static T ReadNetworkObject<T>(this BitStream bs) where T : NetworkObject {
            var id = bs.ReadString();

//             var isEmpty = bs.ReadBool();
//             if (isEmpty)
//                 return null;
// 
//             var chars = new char[32];
//             for (int i = 0; i < 32; ++i) {
//                 chars[i] = (char)bs.ReadByte();
//             }
// 
//             var id = new string(chars);
             return (T)NetworkObjectLookup.instance.CreateFromNetworkAssetId(id);
        }
    }
}