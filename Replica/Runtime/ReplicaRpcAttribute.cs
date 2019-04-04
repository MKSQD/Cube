using System;

namespace Cube.Networking.Replicas {
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ReplicaRpcAttribute : Attribute {
        public RpcTarget type;

        public ReplicaRpcAttribute(RpcTarget type) {
            this.type = type;
        }
    }
}
