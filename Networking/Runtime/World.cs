using Cube.Replication;
using UnityEngine;

namespace Cube.Networking {
    public class World : MonoBehaviour, IReplicaWorld {
        public ClientGame clientGame;
        public IUnityClient client => clientGame ? clientGame.client : null;

        public ServerGame serverGame;
        public IUnityServer server => serverGame ? serverGame.server : null;
    }
}