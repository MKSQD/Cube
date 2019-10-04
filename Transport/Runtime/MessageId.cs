
namespace Cube.Transport {
    public enum MessageId : byte {
        // To Server
        /// <summary>Sent on the server for clients connecting</summary>
        NewConnectionEstablished,
        /// <summary>Sent on the server for clients disconnecting</summary>
        DisconnectNotification,

        // To Client
        ConnectionRequestAccepted,
        ConnectionRequestFailed,

        ReplicaUpdate,
        ReplicaRpc,
        ReplicaDestroy,

        FirstUserId
    };
}
