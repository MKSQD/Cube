#if UNITY_EDITOR && SERVER
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;

namespace Cube.Replication.Editor {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Available in: Editor</remarks>
    [CustomEditor(typeof(ReplicaView))]
    class ReplicaViewInspector : UnityEditor.Editor {
        struct ReplicaDebugInfo {
            public float nextDebugTextUpdateTime;
            public string prorityDescription;
            public float relevance;
            public float finalPriority;
        }

        static GUIStyle[] _debugTextStyles;
        static Dictionary<Replica, ReplicaDebugInfo> _debugPriorities;

        [DrawGizmo(GizmoType.Active | GizmoType.Selected | GizmoType.NotInSelectionHierarchy | GizmoType.Pickable)]
        static void DrawGizmoForReplica(Replica replica, GizmoType gizmoType) {
            if (ReplicaView.Debug == null || !replica.isServer)
                return;

            if (!Selection.Contains(ReplicaView.Debug.gameObject)) {
                ReplicaView.Debug = null;
                return;
            }

            // Setup text stype
            if (_debugTextStyles == null) {
                _debugTextStyles = new GUIStyle[10];
                for (int i = 0; i < 10; ++i) {
                    var style = new GUIStyle();
                    style.fontSize = 10;
                    style.normal.textColor = Color.Lerp(new Color(0, 0.6f, 0, 1), new Color(0.6f, 0, 0, 1), i * 0.1f);
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
                info.nextDebugTextUpdateTime = Time.time + 0.1f;

                info.relevance = replica.GetRelevance(ReplicaView.Debug);

                var idx = ReplicaView.Debug.RelevantReplicas.IndexOf(replica);
                if (idx == -1)
                    return; // Not relevant, exit

                info.finalPriority = ReplicaView.Debug.RelevantReplicaPriorityAccumulator[idx];

                info.prorityDescription = string.Format("{0:0.00}/{1:0.00}/{2}",
                    info.finalPriority,
                    info.relevance,
                    replica.Settings.DesiredUpdateRateMS);

                _debugPriorities[replica] = info;
            }

            // Draw
            var screenPoint = Camera.current.WorldToScreenPoint(replica.transform.position);
            var isVisible = screenPoint.x >= 0 && screenPoint.x < Camera.current.pixelWidth
                && screenPoint.y >= 0 && screenPoint.y < Camera.current.pixelHeight
                && screenPoint.z < 1000 && screenPoint.z > 0;
            if (isVisible) {
                var styleIdx = Mathf.CeilToInt(info.finalPriority * 0.1f * 10); // Expect priority to be in the range [0, 10]
                styleIdx = Math.Max(Math.Min(styleIdx, 9), 0);

                Handles.Label(replica.transform.position, info.prorityDescription, _debugTextStyles[styleIdx]);
            }
        }
    }
}
#endif