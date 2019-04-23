
namespace Cube.Transport {
    public enum MessageId : byte {
        // To Server
        /// <summary>Sent on the server for clients connecting</summary>
        NewConnectionEstablished,
        /// <summary>Sent on the server for clients disconnecting</summary>
        DisconnectNotification,

        LoadSceneDone,

        // To Client
        ConnectionRequestAccepted,
        ConnectionRequestFailed,

        LoadScene,

        ReplicaUpdate,
        ReplicaRpc,
        ReplicaDestroy,

        FirstUserId
    };
}
