using System;

namespace Cube.Networking.Replicas {
    [Flags]
    public enum ApplicationType {
        None = 0x00,
        Client = 0x01,
        Server = 0x02
    }
}
