using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;
using System;

namespace Cube.Replication {
#if SERVER
    public class ReplicaDebugger : EditorWindow {
        [Serializable]
        internal class SimpleTreeViewItem : TreeViewItem {
            public string instances;
            public string bytesPerInstance;
            public string bytesTotal;
        }

        class SimpleTreeView : TreeView {
            public List<ServerReplicaManager.Statistic> data;

            public SimpleTreeView(TreeViewState treeViewState, MultiColumnHeader header)
                : base(treeViewState, header) {
                showAlternatingRowBackgrounds = true;
            }

            protected override TreeViewItem BuildRoot() {
                int nextIdx = 0;

                var root = new TreeViewItem { id = nextIdx++, depth = -1, displayName = "root" };

                if (data != null && data.Count > 0) {
                    for (int i = 0; i < data.Count; ++i) {
                        var statistic = data[i];

                        var serverItem = new SimpleTreeViewItem { id = nextIdx++, displayName = "Server " + i };
                        root.AddChild(serverItem);

                        foreach (var viewInfoPair in statistic.viewInfos) {
                            if (viewInfoPair.view == null)
                                continue;

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
                }
                else {
                    var serverItem = new SimpleTreeViewItem { id = nextIdx++, displayName = "Enter play mode" };
                    root.AddChild(serverItem);
                }

                return root;
            }

            protected override void RowGUI(RowGUIArgs args) {
                var item = (SimpleTreeViewItem)args.item;

                for (int i = 0; i < args.GetNumVisibleColumns(); ++i) {
                    CellGUI(args.GetCellRect(i), item, args.GetColumn(i), ref args);
                }
            }

            void CellGUI(Rect cellRect, SimpleTreeViewItem item, int column, ref RowGUIArgs args) {
                CenterRectUsingSingleLineHeight(ref cellRect);

                switch (column) {
                    case 0: base.RowGUI(args); break;
                    case 1: GUI.Label(cellRect, item.instances); break;
                    case 2: GUI.Label(cellRect, item.bytesPerInstance); break;
                    case 3: GUI.Label(cellRect, item.bytesTotal); break;
                }
            }
        }

        SimpleTreeView _simpleTreeView;
        TreeViewState _treeViewState;

        [MenuItem("Cube/Replica Debugger")]
        public static void ShowWindow() {
            var window = GetWindow(typeof(ReplicaDebugger));
            window.titleContent = new GUIContent("Replica Debugger");
        }

        void OnEnable() {
            if (_simpleTreeView == null) {
                _treeViewState = new TreeViewState();

                var columns = new MultiColumnHeaderState.Column[4] {
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("context"),
                        width = 200
                    },
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("instances"),
                        width = 80,
                        canSort = true
                    },
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("bytes/instance"),
                        width = 100,
                        canSort = true
                    },
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("bytes total"),
                        width = 100,
                        canSort = true
                    }
                };

                var header = new MultiColumnHeader(new MultiColumnHeaderState(columns));

                _simpleTreeView = new SimpleTreeView(_treeViewState, header);
            }
        }

        void OnGUI() {
            if (_simpleTreeView == null)
                return;

            var statistics = new List<ServerReplicaManager.Statistic>();
            foreach (var replicaManager in ServerReplicaManager.all) {
                statistics.Add(replicaManager.statistic);
            }

            _simpleTreeView.data = statistics;
            _simpleTreeView.Reload();

            _simpleTreeView.OnGUI(new Rect(0, 0, position.width, position.height));
        }
    }
#endif
}