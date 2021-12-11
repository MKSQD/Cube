using UnityEngine.SceneManagement;

namespace Cube.Replication {
    public interface IClientReplicaManager : IReplicaManager {
        void ProcessSceneReplicasInScene(Scene scene);

        void Tick();
    }
}
