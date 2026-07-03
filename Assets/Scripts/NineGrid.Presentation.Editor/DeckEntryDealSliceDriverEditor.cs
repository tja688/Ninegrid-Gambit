#if UNITY_EDITOR
using NineGrid.Presentation.Debugging.Slices._Throwaway;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(DeckEntryDealSliceDriver))]
    public sealed class DeckEntryDealSliceDriverEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var driver = (DeckEntryDealSliceDriver)target;
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("牌组入场 + 发牌 垂直切片", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Play 模式下点击「开始测试」：真实 StartNodeCommand → 牌组入场 Flow → 8 张开局发牌 Flow → 批末 Reconcile 解锁。可 Reset 后重复。",
                    MessageType.Info);

                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button("开始测试", GUILayout.Height(32f)))
                {
                    driver.StartTest();
                }

                if (GUILayout.Button("Reset"))
                {
                    driver.Reset();
                }

                EditorGUI.EndDisabledGroup();
            }
        }
    }
}
#endif
