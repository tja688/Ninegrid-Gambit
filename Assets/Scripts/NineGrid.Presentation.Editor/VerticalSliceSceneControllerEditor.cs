#if UNITY_EDITOR
using NineGrid.Presentation.Debugging.Slices;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(VerticalSliceSceneController))]
    public sealed class VerticalSliceSceneControllerEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("PerformanceTest 垂直切片场景", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Play 后自动断开 Shell 主菜单→局内链路，保留真实 CommandGateway。\n"
                    + "用 VerticalSliceTest 上的切片 Driver（如 DeckEntryDealSliceDriver）发 StartNodeCommand；\n"
                    + "面板用上方预设/开关手动控制。MainScene 不挂此组件。",
                    MessageType.Info);

                var controller = (VerticalSliceSceneController)target;
                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button("应用切片模式", GUILayout.Height(28f)))
                {
                    controller.ApplySliceMode();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("局内")) controller.PresetInGame();
                if (GUILayout.Button("主菜单")) controller.PresetMainMenu();
                if (GUILayout.Button("奖励")) controller.PresetReward();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("房间选择")) controller.PresetRoomChoice();
                if (GUILayout.Button("房间事件")) controller.PresetRoomEvent();
                if (GUILayout.Button("结局")) controller.PresetOutcome();
                EditorGUILayout.EndHorizontal();
                EditorGUI.EndDisabledGroup();
            }
        }
    }
}
#endif
