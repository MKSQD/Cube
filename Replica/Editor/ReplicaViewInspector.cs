#if UNITY_EDITOR && SERVER
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Cube.Networking.Replicas {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor</remarks>
    [CustomEditor(typeof(ReplicaView))]
    class ReplicaViewInspector : Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();

            if (Application.isPlaying) {
                var replicaView = (ReplicaView)target;
                if (GUILayout.Button("Debug")) {
                    ReplicaView.debug = replicaView != ReplicaView.debug ? replicaView : null;
                }
            }
        }

        struct ReplicaDebugInfo {
            public float nextDebugTextUpdateTime;
            public string prorityDescription;
            public PriorityResult priorityResult;
        }

        static GUIStyle[] _debugTextStyles;
        static Dictionary<Replica, ReplicaDebugInfo> _debugPriorities;

        [DrawGizmo(GizmoType.Active | GizmoType.Selected | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        static void DrawGizmoForReplica(Replica replica, GizmoType gizmoType) {
            if (ReplicaView.debug == null || !replica.isServer || replica.settings == null)
                return;

            // Setup text stype
            if (_debugTextStyles == null) {
                _debugTextStyles = new GUIStyle[10];
                for (int i = 0; i < 10; ++i) {
                    var style = new GUIStyle();
                    style.normal.textColor = Color.Lerp(Color.green, Color.red, i * 0.1f);
                    _debugTextStyles[i] = style;
                }
            }

            // Update priorities
            if (_debugPriorities == null) {
                _debugPriorities = new Dictionary<Replica, ReplicaDebugInfo>();
            }

            ReplicaDebugInfo info;
            if (!_debugPriorities.TryGetValue(replica, out info)) {
                info = new ReplicaDebugInfo();
            }

            if (Time.time >= info.nextDebugTextUpdateTime) {
                info.nextDebugTextUpdateTime = Time.time + 0.2f;

                info.priorityResult = replica.server.replicaManager.priorityManager.GetPriority(replica, ReplicaView.debug);
                info.prorityDescription = string.Format("{0:0.00}/{1:0.00}/{2}", info.priorityResult.final, info.priorityResult.relevance, replica.settings.desiredUpdateRateMs);

                _debugPriorities[replica] = info;
            }

            // Draw
            var screenPoint = Camera.current.WorldToScreenPoint(replica.transform.position);
            var isVisible = screenPoint.x >= 0 && screenPoint.x < Camera.current.pixelWidth
                && screenPoint.y >= 0 && screenPoint.y < Camera.current.pixelHeight
                && screenPoint.z < replica.settings.maxViewDistance && screenPoint.z > 0;
            if (isVisible) {
                var styleIdx = Mathf.Min(Mathf.CeilToInt(info.priorityResult.final * 10), 9);
                Handles.Label(replica.transform.position, info.prorityDescription, _debugTextStyles[styleIdx]);
            }
        }
    }
}
#endif