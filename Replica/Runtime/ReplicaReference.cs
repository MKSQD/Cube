using System;
using UnityEngine;

namespace Cube.Networking.Replicas {
    [Serializable]
    public class ReplicaReference {
        [SerializeField]
        Replica _replica;

        ReplicaId _id = ReplicaId.Invalid;
        public ReplicaId id {
            set { _id = value; }
        }

        public Replica Resolve(IReplicaManager replicaManager) {
            //#TODO fixme ?
            if (_replica != null) {
                _id = _replica.id;
            }

            return replicaManager.GetReplicaById(_id);
        }
    }
}