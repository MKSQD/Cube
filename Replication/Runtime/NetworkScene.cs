using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Cube.Replication {
    internal class NetworkScene {
        Dictionary<ReplicaId, Replica> _replicasById = new Dictionary<ReplicaId, Replica>();

        List<Replica> _replicas = new List<Replica>();
        public ReadOnlyCollection<Replica> replicas {
            get { return _replicas.AsReadOnly(); }
        }

        public void AddReplica(Replica replica) {
            if (replica.ReplicaId == ReplicaId.Invalid) {
                Debug.LogError("ReplicaId is invalid (" + (replica.isServer ? "Server" : "Client") + ")");
                return;
            }

            if (!_replicasById.ContainsKey(replica.ReplicaId)) {
                _replicasById.Add(replica.ReplicaId, replica);
            }
            else {
                _replicasById[replica.ReplicaId] = replica;
            }
            _replicas.Add(replica);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="replica"></param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        public void RemoveReplica(Replica replica) {
            if (!_replicasById.Remove(replica.ReplicaId))
                return;

            _replicas.Remove(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            Replica replica;
            _replicasById.TryGetValue(id, out replica);
            return replica;
        }

        public void DestroyAll() {
            for (int i = 0; i < _replicas.Count; ++i) {
                var replica = _replicas[i];
                if (replica == null)
                    continue;

                RemoveReplica(replica);
                Object.Destroy(replica.gameObject);
            }
            _replicas.Clear();
        }
    }
}
