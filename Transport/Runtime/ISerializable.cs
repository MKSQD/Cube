namespace Cube.Transport {
    public interface ISerializable {
        void Serialize(BitWriter bs);
        void Deserialize(BitReader bs);
    }
}
