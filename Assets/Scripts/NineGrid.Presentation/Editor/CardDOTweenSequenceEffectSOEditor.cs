#if UNITY_EDITOR
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Editor
{
    [CustomEditor(typeof(CardDOTweenSequenceEffectSO))]
    public sealed class CardDOTweenSequenceEffectSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("从选中对象烘焙 Tween 片段"))
                {
                    var asset = (CardDOTweenSequenceEffectSO)target;
                    var source = Selection.activeGameObject;
                    if (source == null)
                    {
                        Debug.LogWarning("[CardDOTweenSequenceEffectSO] 请在 Hierarchy 中选中源 GameObject。");
                    }
                    else
                    {
                        CardDOTweenSequenceBakeUtility.BakeFromGameObject(source, asset);
                    }
                }
            }
        }
    }
}
#endif
