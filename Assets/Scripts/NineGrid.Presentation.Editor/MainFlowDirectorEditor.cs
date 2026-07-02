#if UNITY_EDITOR
using NineGrid.Presentation.Shell;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(MainFlowDirector))]
    public sealed class MainFlowDirectorEditor : UnityEditor.Editor
    {
        private const string HarnessContextMenuPath = "CONTEXT/MainFlowDirector/测试模式 (Harness)";

        [MenuItem(HarnessContextMenuPath, true, 2)]
        private static bool ValidateHarnessTestMode(MenuCommand command)
        {
            if (command.context is not MainFlowDirector director)
            {
                return false;
            }

            Menu.SetChecked(HarnessContextMenuPath, ReadUseHarness(director));
            return true;
        }

        [MenuItem(HarnessContextMenuPath, false, 2)]
        private static void ToggleHarnessTestMode(MenuCommand command)
        {
            if (command.context is not MainFlowDirector director)
            {
                return;
            }

            SetUseHarness(director, !ReadUseHarness(director));
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var banner = new VisualElement();
            banner.style.marginBottom = 8;
            banner.style.paddingBottom = 8;
            banner.style.borderBottomWidth = 1;
            banner.style.borderBottomColor = new Color(0.22f, 0.18f, 0.14f);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var openConsole = new Button(MainFlowDirectorEditorWindow.ShowWindow)
            {
                text = "打开主流程控制台",
            };
            openConsole.style.flexGrow = 1;
            openConsole.style.height = 28;
            row.Add(openConsole);

            var overflow = new ToolbarMenu { text = "⋮" };
            overflow.style.width = 36;
            overflow.style.height = 28;
            overflow.style.marginLeft = 6;
            overflow.tooltip = "更多操作";
            overflow.menu.AppendAction(
                "表演调试…",
                _ => PerformanceDebugEditorWindow.ShowWindow(),
                DropdownMenuAction.AlwaysEnabled);
            overflow.menu.AppendAction(
                "打开主流程控制台",
                _ => MainFlowDirectorEditorWindow.ShowWindow(),
                DropdownMenuAction.AlwaysEnabled);
            overflow.menu.AppendSeparator();
            overflow.menu.AppendAction(
                "测试模式 (Harness)",
                _ => ToggleHarnessFromInspector(),
                action =>
                {
                    serializedObject.Update();
                    bool enabled = serializedObject.FindProperty("useHarness").boolValue;
                    return enabled
                        ? DropdownMenuAction.Status.Checked | DropdownMenuAction.Status.Normal
                        : DropdownMenuAction.Status.Normal;
                });
            row.Add(overflow);

            banner.Add(row);

            var hint = new Label("Harness 与屏态推进请用主流程控制台；完整 Flow/Shell 模块在表演调试。");
            hint.style.fontSize = 11;
            hint.style.color = new Color(0.79f, 0.73f, 0.67f);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 6;
            banner.Add(hint);

            root.Add(banner);

            var inspector = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject, "m_Script");
                serializedObject.ApplyModifiedProperties();
            });
            root.Add(inspector);

            return root;
        }

        private void ToggleHarnessFromInspector()
        {
            serializedObject.Update();
            SerializedProperty harnessProp = serializedObject.FindProperty("useHarness");
            Undo.RecordObject(target, "Toggle Harness Test Mode");
            harnessProp.boolValue = !harnessProp.boolValue;
            serializedObject.ApplyModifiedProperties();
        }

        private static bool ReadUseHarness(MainFlowDirector director)
        {
            var serializedDirector = new SerializedObject(director);
            return serializedDirector.FindProperty("useHarness").boolValue;
        }

        private static void SetUseHarness(MainFlowDirector director, bool enabled)
        {
            Undo.RecordObject(director, enabled ? "Enable Harness Test Mode" : "Disable Harness Test Mode");
            var serializedDirector = new SerializedObject(director);
            SerializedProperty harnessProp = serializedDirector.FindProperty("useHarness");
            harnessProp.boolValue = enabled;
            serializedDirector.ApplyModifiedProperties();
            EditorUtility.SetDirty(director);
        }
    }
}
#endif
