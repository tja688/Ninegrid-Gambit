#if UNITY_EDITOR
using System.Collections.Generic;
using NineGrid.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StripSpriteCharacterVisual))]
public sealed class StripSpriteCharacterVisualEditor : UnityEditor.Editor
{
    private SerializedProperty catalogProperty;
    private SerializedProperty visualIdProperty;

    private void OnEnable()
    {
        catalogProperty = serializedObject.FindProperty("catalog");
        visualIdProperty = serializedObject.FindProperty("visualId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(catalogProperty);

        var catalog = catalogProperty.objectReferenceValue as StripSpriteVisualCatalog;
        if (catalog != null && catalog.Entries.Count > 0)
        {
            var ids = new List<string>();
            var labels = new List<string>();
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                var entry = catalog.Entries[i];
                ids.Add(entry.Id);
                labels.Add(entry.DisplayName);
            }

            var currentIndex = Mathf.Max(0, ids.IndexOf(visualIdProperty.stringValue));
            var nextIndex = EditorGUILayout.Popup("Visual", currentIndex, labels.ToArray());
            if (nextIndex >= 0 && nextIndex < ids.Count)
            {
                visualIdProperty.stringValue = ids[nextIndex];
            }
        }
        else
        {
            EditorGUILayout.PropertyField(visualIdProperty);
        }

        serializedObject.ApplyModifiedProperties();

        var visual = (StripSpriteCharacterVisual)target;
        if (GUILayout.Button("Apply Visual"))
        {
            visual.ApplyVisual();
            EditorUtility.SetDirty(visual);
        }
    }
}
#endif
