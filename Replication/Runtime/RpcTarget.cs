
namespace Cube.Replication {
    public enum RpcTarget {
        /// Client to server rpc (dropped if the sending client doesn't own the Replica).
        Server,
        /// RPC from the server to the owning client or, if the server owns the Replica, to itself.
        Owner,
        /// RPC from the server to all clients and to itself.
        All,
        /// RPC from the server to all clients except to the owner.
        AllClientsExceptOwner
    }
}
