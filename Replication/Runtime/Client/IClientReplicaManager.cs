using Cube.Transport;

namespace Cube.Replication {
    /// <remarks>Available in: Editor/Client</remarks>
    public interface IClientReplicaManager : IReplicaManager {
        void RemoveReplica(Replica replica);

        void Update();
    }
}
