using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>#69：Luban 工具链已退休；菜单仅保留归档入口说明。</summary>
    public static class ContentVisualLubanMenu
    {
        public const string OpenEditorMenu = "NineGrid/归档/Content Visual Editor";
        public const string RegenerateMenu = "NineGrid/归档/Regenerate Luban（已退休）";

        [MenuItem(OpenEditorMenu)]
        public static void OpenEditor()
        {
            ContentVisualEditorWindow.ShowWindow();
        }

        [MenuItem(RegenerateMenu)]
        public static void RegenerateLuban()
        {
            EditorUtility.DisplayDialog(
                "Luban 已退休",
                "ADR-0008 / #69：Luban 工具链与 Generated 层已移除。\n内容权威为一卡一文件 JSON + ContentVisual/tables。",
                "确定");
        }

        public static bool TryRunGenScript(out string error)
        {
            error = "Luban 工具链已退休（#69）。";
            return false;
        }
    }
}
