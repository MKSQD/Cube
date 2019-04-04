using UnityEngine;
using UnityEngine.Assertions;

namespace Cube.Networking.Replicas {
    public sealed class DefaultReplicaPriorityManager : MonoBehaviour, IReplicaPriorityManager {
        public ReplicaSettings defaultSettings;

        public float minPriorityForSending {
            get { return 0.1f; }
        }

        void Awake() {
            if(defaultSettings == null) {
                Debug.LogWarning("DefaultReplicaPriorityManager: Default ReplicaSettings not set. Initialize default ReplicaSettings object.");
                defaultSettings = ScriptableObject.CreateInstance<ReplicaSettings>();
            }
        }

#if SERVER
        public PriorityResult GetPriority(Replica replica, ReplicaView view) {
            var settings = replica.settings == null ? defaultSettings : replica.settings;

            //
            var distanceRelevance = 1f;
            if ((settings.priorityFlags & ReplicaPriorityFlag.IgnorePosition) == 0 && !view.ignoreReplicaPositionsForPriority) {
                var sqrMaxDist = Mathf.Pow(settings.maxViewDistance, 2);

                var sqrDist = Mathf.Pow(replica.transform.position.x - view.transform.position.x, 2)
                    + Mathf.Pow(replica.transform.position.z - view.transform.position.z, 2);
                if (sqrDist > sqrMaxDist)
                    return new PriorityResult() {
                        relevance = 0,
                        final = 0
                    }; // No costly calculations

                distanceRelevance = 1f - sqrDist / sqrMaxDist;
                Assert.IsTrue(distanceRelevance >= 0 && distanceRelevance <= 1);
            }

            var relevance = distanceRelevance;
            Assert.IsTrue(relevance >= 0 && relevance <= 1);

            //
            ReplicaView.UpdateInfo updateInfo;
            view.replicaUpdateInfo.TryGetValue(replica, out updateInfo);

            var lastUpdatePriority = (Time.time - updateInfo.lastUpdateTime) / (settings.desiredUpdateRateMs * 0.001f);
            lastUpdatePriority = Mathf.Min(lastUpdatePriority, 1);

            //
            return new PriorityResult {
                relevance = relevance,
                final = relevance * lastUpdatePriority
            };
        }
#endif
    }
}