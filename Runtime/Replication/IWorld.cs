using UnityEngine;

namespace Cube.Replication {
    /// <summary>
    /// Can be used to store per game instance data. To access it in a ReplicaBehaviour, use server.world or client.world.
    /// Downcast to your actual derived class like so:
    /// var world = (MyGameWorld)server.world;
    /// </summary>
    public interface IWorld {
        Transform transform {
            get;
        }
    }
}