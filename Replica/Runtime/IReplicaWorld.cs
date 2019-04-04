using UnityEngine;

namespace Cube.Networking.Replicas {
    public interface IReplicaWorld {
        Transform transform {
            get;
        }

#if CLIENT
        IUnityClient client {
            get;
        }
#endif
#if SERVER
        IUnityServer server {
            get;
        }
#endif
    }
}