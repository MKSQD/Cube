
namespace Cube.Transport {
    public enum MessageId : byte {
        // To Client
        ReplicaUpdate,
        ReplicaRpc,
        ReplicaDestroy,

        FirstUserId
    };
}
