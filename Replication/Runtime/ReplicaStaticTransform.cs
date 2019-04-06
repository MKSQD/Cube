using UnityEngine;
using BitStream = Cube.Transport.BitStream;

namespace Cube.Replication {
    /// <summary>
    /// No support for parent transforms.
    /// </summary>
    [AddComponentMenu("Cube/ReplicaStaticTransform")]
    class ReplicaStaticTransform : ReplicaBehaviour {
#if SERVER
        public override void Serialize(BitStream bs, ReplicaSerializationMode mode, ReplicaView view) {
            if (mode == ReplicaSerializationMode.Partial)
                return;

            bs.Write(transform.localPosition);
            bs.Write(transform.localRotation);
        }
#endif

#if CLIENT
        public override void Deserialize(BitStream bs, ReplicaSerializationMode mode) {
            if (mode == ReplicaSerializationMode.Partial)
                return;

            transform.localPosition = bs.ReadVector3();
            transform.localRotation = bs.ReadQuaternion();
        }
#endif
    }
}
