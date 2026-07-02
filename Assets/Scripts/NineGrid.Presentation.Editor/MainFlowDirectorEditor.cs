#if UNITY_EDITOR
using NineGrid.Presentation.Shell;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(MainFlowDirector))]
    public sealed class MainFlowDirectorEditor : UnityEditor.Editor
    {
        [MenuItem("CONTEXT/MainFlowDirector/打开表演调试", false, 0)]
        private static void OpenPerformanceDebugFromContext(MenuCommand command)
        {
            PerformanceDebugEditorWindow.ShowWindow();
            if (command.context is MainFlowDirector director)
            {
                Selection.activeObject = director;
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Play 后选中 MainFlowHarnessDriver，点「开启主流程测试」进入轻量测试流程。",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
#endif
