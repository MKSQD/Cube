namespace Cube.Transport {
    public interface IBitSerializable {
        void Serialize(IBitWriter bs);
        void Deserialize(BitReader bs);
    }
}
