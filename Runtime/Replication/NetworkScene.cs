using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cube.Replication {
    internal class NetworkScene {
        public ReadOnlyCollection<Replica> Replicas => replicas.AsReadOnly();

        Dictionary<ReplicaId, Replica> replicasById = new Dictionary<ReplicaId, Replica>();
        List<Replica> replicas = new List<Replica>();

        public void AddReplica(Replica replica) {
            if (replica.Id == ReplicaId.Invalid) {
                Debug.LogError("ReplicaId is invalid (" + (replica.isServer ? "Server" : "Client") + ")");
                return;
            }

            if (!replicasById.ContainsKey(replica.Id)) {
                replicasById.Add(replica.Id, replica);
            } else {
                replicasById[replica.Id] = replica;
            }
            replicas.Add(replica);
        }
        
        public void RemoveReplica(Replica replica) {
            if (!replicasById.Remove(replica.Id))
                return;

            replicas.Remove(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            replicasById.TryGetValue(id, out Replica replica);
            return replica;
        }

        public void DestroyAll() {
            for (int i = 0; i < replicas.Count; ++i) {
                var replica = replicas[i];
                if (replica == null)
                    continue;

                RemoveReplica(replica);
                Object.Destroy(replica.gameObject);
            }
            replicas.Clear();
        }
    }
}
