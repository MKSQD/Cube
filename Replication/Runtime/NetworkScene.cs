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
            if (replica.id == ReplicaId.Invalid) {
                Debug.LogError("ReplicaId is invalid (" + (replica.isServer ? "Server" : "Client") + ")");
                return;
            }

            if (_replicasById.ContainsKey(replica.id)) {
                Debug.LogError("ReplicaId " + replica.id + " (" + replica.gameObject.name + ") already known on (" + (replica.isServer ? "Server" : "Client") + ")");
                return;
            }

            _replicasById.Add(replica.id, replica);
            _replicas.Add(replica);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="replica"></param>
        /// <remarks>Won't do anything if this replica was removed already</remarks>
        public void RemoveReplica(Replica replica) {
            if (!_replicasById.Remove(replica.id))
                return;

            _replicas.Remove(replica);
        }

        public Replica GetReplicaById(ReplicaId id) {
            Replica replica;
            _replicasById.TryGetValue(id, out replica);
            return replica;
        }
    }
}
