
namespace Cube.Transport {
    public enum MessageId : byte {
        // Server
        /// <summary>Sent on the server for clients connecting</summary>
        NewConnectionEstablished,
        /// <summary>Sent on the server for clients disconnecting</summary>
        DisconnectNotification,

        // Client
        ConnectionRequestAccepted,
        ConnectionRequestFailed,

        ReplicaFullUpdate,
        ReplicaPartialUpdate,
        ReplicaRpc,
        ReplicaDestroy,

        FirstUserId
    };
}
