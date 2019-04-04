using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;

namespace Cube.Networking.Replicas {
#if SERVER
    [Serializable]
    internal class SimpleTreeViewItem : TreeViewItem {
        public string instances;
        public string bytesPerInstance;
        public string bytesTotal;
    }

    class SimpleTreeView : TreeView {
        List<ServerReplicaManager.Statistic> _data;

        public SimpleTreeView(TreeViewState treeViewState, MultiColumnHeader header, List<ServerReplicaManager.Statistic> data)
            : base(treeViewState, header) {
            _data = data;

            showAlternatingRowBackgrounds = true;

            Reload();
        }

        protected override TreeViewItem BuildRoot() {
            int nextIdx = 0;

            var root = new TreeViewItem { id = nextIdx++, depth = -1, displayName = "root" };

            for (int i = 0; i < _data.Count; ++i) {
                var statistic = _data[i];

                var serverItem = new SimpleTreeViewItem { id = nextIdx++, displayName = "Server " + i };
                root.AddChild(serverItem);

                foreach (var viewInfoPair in statistic.viewInfos) {
                    var replicaViewItem = new SimpleTreeViewItem { id = nextIdx++, displayName = viewInfoPair.view.name };
                    serverItem.AddChild(replicaViewItem);

                    foreach (var info in viewInfoPair.info.bytesPerPrefabIdx) {
                        var name = info.Key.ToString();

                        GameObject prefab;
                        if (NetworkPrefabLookup.instance.TryGetClientPrefabForIndex(info.Key, out prefab)) {
                            name = prefab.name;
                        }

                        var infoItem = new SimpleTreeViewItem {
                            id = nextIdx++,
                            displayName = name,
                            instances = info.Value.numInstances.ToString(),
                            bytesPerInstance = (info.Value.totalBytes / (float)info.Value.numInstances).ToString(),
                            bytesTotal = info.Value.totalBytes.ToString()
                        };

                        replicaViewItem.AddChild(infoItem);
                    }
                }
            }

            SetupDepthsFromParentsAndChildren(root);

            return root;
        }

        protected override void RowGUI(RowGUIArgs args) {
            var item = (SimpleTreeViewItem)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i) {
                CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, SimpleTreeViewItem item, int column, ref RowGUIArgs args) {
            // Center the cell rect vertically using EditorGUIUtility.singleLineHeight.
            // This makes it easier to place controls and icons in the cells.
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column) {

                case 0:
                    base.RowGUI(args);
                    break;

                case 1:
                    GUI.Label(cellRect, item.instances);
                    break;

                case 2:
                    GUI.Label(cellRect, item.bytesPerInstance);
                    break;

                case 3:
                    GUI.Label(cellRect, item.bytesTotal);
                    break;
            }
        }
    }

    public class ReplicaDebugger : EditorWindow {
        SimpleTreeView _simpleTreeView;
        TreeViewState _treeViewState;

        [MenuItem("Window/Analysis/Network ReplicaDebugger")]
        [MenuItem("Cube/Window/Network ReplicaDebugger")]
        public static void ShowWindow() {
            var window = GetWindow(typeof(ReplicaDebugger));
            window.titleContent = new GUIContent("ReplicaDebugger");
        }

        void OnEnable() {
            if (_treeViewState == null) {
                _treeViewState = new TreeViewState();
            }

            EditorApplication.pauseStateChanged += OnPause;
        }

        void OnGUI() {
            if (!Application.isPlaying) {
                EditorGUILayout.LabelField(new GUIContent("Enter play mode to use"));
                return;
            }

            if (_simpleTreeView == null) {
                EditorGUILayout.LabelField(new GUIContent("Pause game to use"));
                return;
            }

            _simpleTreeView.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        void OnPause(PauseState state) {
            if (state != PauseState.Paused)
                return;

            var statistics = new List<ServerReplicaManager.Statistic>();

            throw new Exception("Fixme");
//             foreach (var server in UnityServer.all) {
//                 var replicaManager = server.replicaManager as ServerReplicaManager;
//                 if (replicaManager == null)
//                     continue;
// 
//                 statistics.Add(replicaManager.statistic);
//             }
// 
//             var columns = new MultiColumnHeaderState.Column[4];
//             columns[0] = new MultiColumnHeaderState.Column() {
//                 headerContent = new GUIContent("context"),
//                 width = 300
//             };
//             columns[1] = new MultiColumnHeaderState.Column() {
//                 headerContent = new GUIContent("instances"),
//                 width = 80,
//                 canSort = true
//             };
//             columns[2] = new MultiColumnHeaderState.Column() {
//                 headerContent = new GUIContent("bytes/instance"),
//                 width = 100,
//                 canSort = true
//             };
//             columns[3] = new MultiColumnHeaderState.Column() {
//                 headerContent = new GUIContent("bytes total"),
//                 width = 100,
//                 canSort = true
//             };
// 
//             var header = new MultiColumnHeader(new MultiColumnHeaderState(columns));
//             _simpleTreeView = new SimpleTreeView(_treeViewState, header, statistics);
// 
//             Repaint();
        }
    }
#endif
}