
namespace Cube.Transport {
    public enum MessageId : byte {
        // Server -> Client
        LoadMap,

        ReplicaUpdate,
        ReplicaRpc,
        ReplicaDestroy,

        // Client -> Server
        LoadSceneDone,

        FirstUserId
    };
}
