#if UNITY_EDITOR
using NineGrid.Presentation.Shell;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(MainFlowHarnessDriver))]
    public sealed class MainFlowHarnessDriverEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var harness = (MainFlowHarnessDriver)target;
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("主流程测试", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "开启后回主菜单，正常点开始 → 节点 1s 自动结算 → 选手牌 → 左房循环；右房走事件+一轮节点后跳过奖励直接结局。",
                    MessageType.Info);

                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button("开启主流程测试", GUILayout.Height(32f)))
                {
                    harness.EnableTestFlow();
                }

                EditorGUI.BeginDisabledGroup(!harness.IsActive);
                if (GUILayout.Button("关闭主流程测试"))
                {
                    harness.DisableTestFlow();
                }
                EditorGUI.EndDisabledGroup();
                EditorGUI.EndDisabledGroup();

                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("状态", harness.IsActive ? "已开启" : "未开启");
                }
            }
        }
    }
}
#endif
