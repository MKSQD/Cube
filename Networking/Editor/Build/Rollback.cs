using System;

namespace Cube {
    public class Rollback : IDisposable {
        Action _cleanup;

        public Rollback(Action cleanup) {
            _cleanup = cleanup;
        }

        void IDisposable.Dispose() {
            if (_cleanup != null)
                _cleanup();
        }
    }
}
