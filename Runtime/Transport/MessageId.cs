
namespace Cube.Transport {
    public enum MessageId : byte {
        // Server -> Client
        LoadScene,

        ReplicaUpdate,
        ReplicaRpc,
        ReplicaDestroy,

        // Client -> Server
        LoadSceneDone,

        FirstUserId
    };
}
