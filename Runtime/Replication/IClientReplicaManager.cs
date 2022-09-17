using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public interface IClientReplicaManager : IReplicaManager {
        void AddReplica(Replica replica);

        void ProcessSceneReplicasInScene(Scene scene);

        void Tick();
    }
}
