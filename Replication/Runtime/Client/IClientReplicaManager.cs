using Cube.Transport;

namespace Cube.Replication {
#if CLIENT
    /// <remarks>Available in: Editor/Client</remarks>
    public interface IClientReplicaManager : IReplicaManager {
        IClientReactor reactor {
            get;
        }

        void RemoveReplica(Replica replica);

        void Update();
    }
#endif
}
