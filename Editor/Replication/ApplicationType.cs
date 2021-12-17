using System;

namespace Cube.Replication {
    [Flags]
    public enum ApplicationType {
        None = 0,
        Client = 1,
        Server = 2
    }
}
