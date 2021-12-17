
namespace Cube.Transport {
    public enum PacketReliability {
        Unreliable,
        UnreliableSequenced,

        ReliableUnordered,
        ReliableOrdered,
        ReliableSequenced
    }
}
