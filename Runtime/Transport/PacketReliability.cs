
namespace Cube.Transport {
    public enum PacketReliability {
        /// <summary>
        /// Simple UDP packets without order and reliability
        /// </summary>
        Unreliable,
        /// <summary>
        /// Ordered but unreliable with duplication prevention
        /// </summary>
        UnreliableSequenced,

        /// <summary>
        /// Reliable without order
        /// </summary>
        ReliableUnordered,
        /// <summary>
        /// Reliable with order
        /// </summary>
        ReliableOrdered,
        /// <summary>
        /// Reliable sequenced (reliable only last packet)
        /// </summary>
        ReliableSequenced
    }
}
