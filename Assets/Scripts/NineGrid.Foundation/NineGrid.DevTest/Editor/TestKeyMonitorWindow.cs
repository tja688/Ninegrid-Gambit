#if UNITY_EDITOR

using System.Collections.Generic;
using System.Text;
using NineGrid.DevTest;
using UnityEditor;
using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    public sealed class TestKeyMonitorWindow : EditorWindow
    {
        private Vector2 _scroll;
        private bool _autoRefresh = true;

        [MenuItem("Window/NineGrid/Test Key Monitor")]
        public static void Open()
        {
            GetWindow<TestKeyMonitorWindow>("Test Key Monitor");
        }

        private void OnEnable()
        {
            TestKeyManager.Instance.Changed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            TestKeyManager.Instance.Changed -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            Repaint();
        }

        private void OnGUI()
        {
            _autoRefresh = EditorGUILayout.ToggleLeft("自动刷新", _autoRefresh);

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后显示实时级联归属。", MessageType.Info);
            }

            var manager = TestKeyManager.Instance;
            DrawStack(manager);
            EditorGUILayout.Space(8f);
            DrawResolvedBindings(manager);
        }

        private void DrawStack(TestKeyManager manager)
        {
            EditorGUILayout.LabelField("级联栈（下 = 高优先级；顺序以 TestKeyStackConfigSO 为准）", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            var order = manager.StackOrder;
            if (order.Count == 0)
            {
                EditorGUILayout.LabelField("(空)");
            }
            else
            {
                for (var i = 0; i < order.Count; i++)
                {
                    var layerId = order[i];
                    manager.Layers.TryGetValue(layerId, out var state);
                    var isTop = i == order.Count - 1;
                    var style = isTop ? EditorStyles.boldLabel : EditorStyles.label;
                    var suffix = isTop ? "  ← 栈顶/最高优先级" : string.Empty;

                    EditorGUILayout.LabelField($"[{i}] {state?.DisplayName ?? layerId}{suffix}", style);

                    if (state != null && GUILayout.Button("写入 SO 并置顶", GUILayout.Width(140f)))
                    {
                        manager.PromoteLayerToTop(layerId);
                    }

                    if (state != null)
                    {
                        DrawLayerKeys(manager, layerId, state);
                    }

                    EditorGUILayout.Space(4f);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawLayerKeys(TestKeyManager manager, string layerId, TestKeyLayerRuntimeState state)
        {
            var active = manager.GetActiveKeysForLayer(layerId);
            var overflow = manager.GetOverflowKeysForLayer(layerId);

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"声明 {state.Bindings.Count} 键 | 生效 {active.Count} | 溢出 {overflow.Count}");

            foreach (var pair in state.Bindings)
            {
                var line = new StringBuilder()
                    .Append(pair.Key)
                    .Append(" — ")
                    .Append(pair.Value.Label);

                if (manager.TryGetOwner(pair.Key, out var owner) && owner == layerId)
                {
                    EditorGUILayout.LabelField(line.ToString(), EditorStyles.label);
                }
                else
                {
                    EditorGUILayout.LabelField(line + " (溢出)", EditorStyles.miniLabel);
                }
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawResolvedBindings(TestKeyManager manager)
        {
            EditorGUILayout.LabelField("当前生效表", EditorStyles.boldLabel);

            if (manager.ActiveBindings.Count == 0)
            {
                EditorGUILayout.LabelField("(无)");
                return;
            }

            foreach (var pair in manager.ActiveBindings)
            {
                EditorGUILayout.LabelField($"{pair.Key} → {pair.Value.LayerId} ({pair.Value.Label})");
            }
        }
    }
}

#endif
