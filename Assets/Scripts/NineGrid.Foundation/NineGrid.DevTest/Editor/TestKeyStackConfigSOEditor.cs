#if UNITY_EDITOR

using NineGrid.DevTest;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NineGrid.DevTest.Editor
{
    [CustomEditor(typeof(TestKeyStackConfigSO))]
    public sealed class TestKeyStackConfigSOEditor : UnityEditor.Editor
    {
        private ReorderableList _layerList;

        private void OnEnable()
        {
            var layersProp = serializedObject.FindProperty("layers");
            _layerList = new ReorderableList(serializedObject, layersProp, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(
                        rect,
                        "级联层（上→低优先级，下→高优先级；最底项永远优先激活）");
                },
                drawElementCallback = (rect, index, _, _) =>
                {
                    var element = layersProp.GetArrayElementAtIndex(index);
                    rect.y += 2f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, element, GUIContent.none);
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            _layerList.DoLayoutList();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "级联溢出：自栈底向上分配按键。某层未拿到的同键绑定会显示为「溢出」。" +
                "运行时可打开 Window > NineGrid > Test Key Monitor 查看实时归属。",
                MessageType.Info);

            if (GUILayout.Button("将选中层置顶（移到底部）"))
            {
                var stack = (TestKeyStackConfigSO)target;
                var selected = Selection.activeObject as TestKeyLayerProfileSO;
                if (selected != null)
                {
                    Undo.RecordObject(stack, "Promote Test Key Layer");
                    stack.PromoteLayer(selected);
                    EditorUtility.SetDirty(stack);
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Test Key Stack",
                        "请先在 Project 窗口选中一个 TestKeyLayerProfileSO。",
                        "OK");
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
