#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System;
using System.Collections.Generic;

namespace Cube.Transport {
    public class TransportDebugger : EditorWindow {
        [Serializable]
        internal class SimpleTreeViewItem : TreeViewItem {
            public string Bits;
        }

        public class Frame {
            public string Name;
            public int Bits;
            public List<Frame> Children;
            public Frame Parent;
        }

        public static readonly int MaxFrames = 10;
        public int CurrentFrameIdx = 0;
        public List<Frame> Frames;
        public Frame CurrentFrame;
        public List<string> Statistics = new List<string>();

        class SimpleTreeView : TreeView {
            public TransportDebugger Debugger;

            public SimpleTreeView(TreeViewState treeViewState, MultiColumnHeader header)
                : base(treeViewState, header) {
                showAlternatingRowBackgrounds = true;
            }

            protected override TreeViewItem BuildRoot() {
                var root = new TreeViewItem {
                    id = 0,
                    depth = -1,
                    displayName = "root"
                };

                var frames = Debugger.Frames;
                if (frames == null) {
                    var serverItem = new SimpleTreeViewItem { id = 1, displayName = "No data" };
                    root.AddChild(serverItem);
                    return root;
                }

                int nextIdx = 1;
                for (int i = 0; i < MaxFrames; ++i) {
                    var idx = Debugger.CurrentFrameIdx - i;
                    if (idx < 0) {
                        idx = MaxFrames - 1;
                    }

                    var currentFrame = frames[idx];
                    currentFrame.Name = "Frame N-" + i;
                    Foo(currentFrame, root, ref nextIdx);
                }

                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            int Foo(Frame frame, TreeViewItem parent, ref int nextIdx) {
                var infoItem = new SimpleTreeViewItem {
                    id = nextIdx++,
                    displayName = frame.Name
                };

                if (frame.Children != null) {
                    var bits = 0;
                    foreach (var child in frame.Children) {
                        bits += Foo(child, infoItem, ref nextIdx);
                    }
                    if (frame.Bits == 0) {
                        frame.Bits = bits;
                    }
                }

                infoItem.Bits = frame.Bits.ToString();
                parent.AddChild(infoItem);

                return frame.Bits;
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
                    case 0:
                        base.RowGUI(args);
                        break;
                    case 1:
                        GUI.Label(cellRect, item.Bits);
                        break;
                }
            }
        }

        SimpleTreeView simpleTreeView;
        TreeViewState treeViewState;
        static TransportDebugger activeDebugger;

        [MenuItem("Cube/Transport Debugger")]
        public static void ShowWindow() {
            var debugger = GetWindow<TransportDebugger>();
            debugger.titleContent = new GUIContent("Transport Debugger");
        }

        public static void CycleFrame() {
            if (activeDebugger == null)
                return;

            activeDebugger.CurrentFrameIdx = (activeDebugger.CurrentFrameIdx + 1) % MaxFrames;
            activeDebugger.Statistics.Clear();

            if (activeDebugger.Frames == null) {
                activeDebugger.Frames = new List<Frame>();
                while (activeDebugger.Frames.Count < MaxFrames) {
                    activeDebugger.Frames.Add(new Frame() { Children = new List<Frame>() });
                }
            }

            var currentFrame = activeDebugger.Frames[activeDebugger.CurrentFrameIdx];
            currentFrame.Name = "";
            currentFrame.Bits = 0;
            currentFrame.Children.Clear();

            activeDebugger.CurrentFrame = currentFrame;
        }

        public static void BeginScope(string name) {
            if (activeDebugger == null)
                return;

            var newChild = new Frame {
                Parent = activeDebugger.CurrentFrame,
                Name = name
            };

            if (activeDebugger.CurrentFrame.Children == null) {
                activeDebugger.CurrentFrame.Children = new List<Frame>();
            }

            activeDebugger.CurrentFrame.Children.Add(newChild);
            activeDebugger.CurrentFrame = newChild;
        }

        public static void EndScope(int numBits = 0) {
            if (activeDebugger == null)
                return;

            activeDebugger.CurrentFrame.Bits = numBits;
            activeDebugger.CurrentFrame = activeDebugger.CurrentFrame.Parent;
        }

        public static void ReportStatistic(string line) {
            if (activeDebugger == null)
                return;

            activeDebugger.Statistics.Add(line);
        }

        void OnEnable() {
            if (simpleTreeView != null)
                return;

            var columns = new MultiColumnHeaderState.Column[] {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Name"), width = 500 },
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Bits"), width = 100, canSort = true }
            };

            var header = new MultiColumnHeader(new MultiColumnHeaderState(columns));
            treeViewState = new TreeViewState();
            simpleTreeView = new SimpleTreeView(treeViewState, header);
        }

        int tab;
        void OnGUI() {
            if (simpleTreeView == null)
                return;

            if (CurrentFrame == null) {
                CurrentFrame = new Frame();
            }

            activeDebugger = this;

            tab = GUILayout.Toolbar(tab, new string[] { "Frames", "Server Statistics" });
            switch (tab) {
                case 0:
                    simpleTreeView.Debugger = this;
                    simpleTreeView.Reload();
                    simpleTreeView.OnGUI(new Rect(0, 20, position.width, position.height));
                    break;

                case 1:
                    foreach (var line in Statistics) {
                        GUILayout.Label(line);
                    }
                    Repaint();
                    break;
            }
        }
    }
}
#endif