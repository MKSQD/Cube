namespace Cube.Transport {
    public interface ISerializable {
        void Serialize(BitStream bs);
        void Deserialize(BitStream bs);
    }
}
