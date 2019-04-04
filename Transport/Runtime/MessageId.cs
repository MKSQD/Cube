
namespace Cube.Networking.Transport {
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

        // Client.Gameplay
        ControllerLocalPawnPossess, // Server -> Client
        ControllerLocalPawnUnpossess, // Server -> Client
        ControllerInput, // Client -> Server
        ControllerResetPawnPosition, // Server -> Client
        SetGameState, // Server -> Client

        FirstUserId
    };
}
