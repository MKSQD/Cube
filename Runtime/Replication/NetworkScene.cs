using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;

namespace Cube.Replication {
    internal class NetworkScene {
        public ReadOnlyCollection<Replica> Replicas => _replicas.AsReadOnly();

        Dictionary<ReplicaId, Replica> _replicasById = new();
        List<Replica> _replicas = new();

        public void AddReplica(Replica replica) {
            if (replica.Id == ReplicaId.Invalid)
                throw new Exception("ReplicaId is invalid (" + (replica.isServer ? "Server" : "Client") + ")");

            if (!_replicasById.ContainsKey(replica.Id)) {
                _replicasById.Add(replica.Id, replica);
            } else {
                _replicasById[replica.Id] = replica;
            }
            _replicas.Add(replica);
        }

        public void RemoveReplica(Replica replica) {
            if (!_replicasById.Remove(replica.Id))
                return;

            _replicas.Remove(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            _replicasById.TryGetValue(id, out Replica replica);
            return replica;
        }

        public void DestroyAll() {
            for (int i = 0; i < _replicas.Count; ++i) {
                var replica = _replicas[i];
                if (replica == null)
                    continue;

                RemoveReplica(replica);
                UnityEngine.Object.Destroy(replica.gameObject);
            }
            _replicas.Clear();
        }
    }
}
