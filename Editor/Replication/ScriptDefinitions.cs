using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Cube {
    /// <summary>
    /// Simplifies editing of <see href="https://docs.unity3d.com/Manual/PlatformDependentCompilation.html">Scripting Define Symbols</see>
    /// </summary>
    public class ScriptDefinitions {
        public struct Snapshot {
            public List<String> definitions;
        }

        BuildTargetGroup _target;

        List<String> _definitions;
        public List<String> definitions { get { return _definitions; } }

        public ScriptDefinitions(BuildTargetGroup target) {
            _target = target;
            _definitions = Load(_target);
        }

        List<String> Load(BuildTargetGroup target) {
            var currentSymbolString = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            return currentSymbolString.Split(';').ToList();
        }

        public void Set(string name, bool set) {
            if (set) Add(name);
            else Remove(name);
        }

        public void Add(List<string> names) {
            foreach (var name in names)
                Add(name);
        }

        public void Add(string name) {
            if (!IsSet(name))
                _definitions.Add(name);
        }

        public void Remove(string name) {
            _definitions.Remove(name);
        }

        public bool IsSet(string name) {
            return _definitions.Contains(name);
        }

        public void Clear() {
            _definitions.Clear();
        }

        /// <summary>
        ///  
        /// </summary>
        /// <remarks>Will trigger an assembly refresh</remarks>
        public void Write() {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(_target, string.Join(";", _definitions.ToArray()));
        }

        /// <summary>
        /// Takes a snapshot of the currently loaded definitions
        /// </summary>
        public Snapshot TakeSnapshot() {
            var snapshot = new Snapshot();
            snapshot.definitions = new List<string>(_definitions);
            return snapshot;
        }

        /// <summary>
        /// Revert the definitions to a previously saved snapshot
        /// </summary>
        /// <remarks>To apply changed, you must call Write() anyway</remarks>
        public void SetSnapshot(Snapshot snapshot) {
            _definitions = snapshot.definitions;
        }

        public override string ToString() {
            return string.Join(";", _definitions.ToArray());
        }

    }
}