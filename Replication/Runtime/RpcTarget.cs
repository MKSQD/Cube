
namespace Cube.Replication {
    public enum RpcTarget {
        /// <summary>
        /// Client to server rpc (dropped if the sending client doesn't own the Replica).
        /// </summary>
        Server,

        /// <summary>
        /// RPC from the server to the owning client or, if the server owns the Replica, to itself.
        /// </summary>
        Owner,

        /// <summary>
        /// RPC from the server to all clients and to itself.
        /// </summary>
        All
    }
}
