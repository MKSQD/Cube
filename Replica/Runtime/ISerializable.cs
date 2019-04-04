using Cube.Networking.Transport;

namespace Cube.Networking.Replicas {
    /// <remarks>Available in: Editor/Client/Server</remarks>
    public interface ISerializable {
        void Serialize(BitStream bs);
        void Deserialize(BitStream bs);
    }
}
