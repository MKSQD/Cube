using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Collections.Generic;

namespace Cube.Replication {
    public class ReplicaDebugger : EditorWindow {
        [Serializable]
        internal class SimpleTreeViewItem : TreeViewItem {
            public string instances;
            public string bytesPerInstance;
            public string bytesTotal;
            public string numRpcs;
            public string rpcBytes;
        }

        class SimpleTreeView : TreeView {
            public ServerReplicaManagerStatistics Statistics;

            public SimpleTreeView(TreeViewState treeViewState, MultiColumnHeader header)
                : base(treeViewState, header) {
                showAlternatingRowBackgrounds = true;
            }

            protected override TreeViewItem BuildRoot() {
                int nextIdx = 0;

                var root = new TreeViewItem { id = nextIdx++, depth = -1, displayName = "root" };

                if (Statistics == null) {
                    var serverItem = new SimpleTreeViewItem { id = nextIdx++, displayName = "N" };
                    root.AddChild(serverItem);
                    return root;
                }

                foreach (var viewInfoPair in Statistics.ViewInfos) {
                    if (viewInfoPair.View == null)
                        continue;

                    var replicaViewItem = new SimpleTreeViewItem { id = nextIdx++, displayName = viewInfoPair.View.name };
                    root.AddChild(replicaViewItem);

                    foreach (var info in viewInfoPair.Info.ReplicaTypeInfos) {
                        var name = info.Key.ToString();

                        GameObject prefab;
                        if (NetworkPrefabLookup.instance.TryGetClientPrefabForIndex(info.Key, out prefab)) {
                            name = prefab.name;
                        }

                        var replicaTypeInfo = info.Value;
                        var infoItem = new SimpleTreeViewItem {
                            id = nextIdx++,
                            displayName = name,
                            instances = replicaTypeInfo.NumInstances.ToString(),
                            bytesPerInstance = (replicaTypeInfo.TotalBytes / (float)replicaTypeInfo.NumInstances).ToString(),
                            bytesTotal = replicaTypeInfo.TotalBytes.ToString(),
                            numRpcs = replicaTypeInfo.NumRpcs.ToString(),
                            rpcBytes = replicaTypeInfo.RpcBytes.ToString()
                        };

                        replicaViewItem.AddChild(infoItem);
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
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column) {
                case 0: base.RowGUI(args); break;
                case 1: GUI.Label(cellRect, item.instances); break;
                case 2: GUI.Label(cellRect, item.bytesPerInstance); break;
                case 3: GUI.Label(cellRect, item.bytesTotal); break;
                case 4: GUI.Label(cellRect, item.numRpcs); break;
                case 5: GUI.Label(cellRect, item.rpcBytes); break;
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
        if (_simpleTreeView != null)
            return;

        _treeViewState = new TreeViewState();

        var columns = new MultiColumnHeaderState.Column[6] {
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
                    },
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("rpcs"),
                        width = 100,
                        canSort = true
                    },
                    new MultiColumnHeaderState.Column() {
                        headerContent = new GUIContent("rpc bytes"),
                        width = 100,
                        canSort = true
                    }
                };

        var header = new MultiColumnHeader(new MultiColumnHeaderState(columns));
        _simpleTreeView = new SimpleTreeView(_treeViewState, header);
    }

    void OnGUI() {
        if (_simpleTreeView == null)
            return;

        _simpleTreeView.Statistics = ServerReplicaManager.Main.Statistics;
        _simpleTreeView.Reload();

        _simpleTreeView.OnGUI(new Rect(0, 0, position.width, position.height));
    }
}
}