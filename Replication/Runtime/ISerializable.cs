using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <remarks>Available in: Editor/Client/Server</remarks>
    public interface ISerializable {
        void Serialize(BitStream bs);
        void Deserialize(BitStream bs);
    }
}
