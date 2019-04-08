using System;
using UnityEditor;

namespace Cube {
    public class ReloadAssembiesLocker : IDisposable {
        public ReloadAssembiesLocker() {
            EditorApplication.LockReloadAssemblies();
        }

        public void Dispose() {
            EditorApplication.UnlockReloadAssemblies();
        }
    }
}
