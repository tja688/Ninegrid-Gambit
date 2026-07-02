#if UNITY_EDITOR
using NineGrid.Presentation.Shell;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(MainFlowDirector))]
    public sealed class MainFlowDirectorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Play 后可选挂载 MainFlowHarnessDriver，点「开启主流程测试」进入轻量测试流程。",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
#endif
