#if UNITY_EDITOR
using System.Collections.Generic;
using NineGrid.Data;
using NineGrid.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Editor
{
    [CustomEditor(typeof(EnemyShipDataSO))]
    public sealed class EnemyShipDataSOEditor : UnityEditor.Editor
    {
        SerializedProperty visualCatalogProperty;
        SerializedProperty visualIdProperty;

        void OnEnable()
        {
            visualCatalogProperty = serializedObject.FindProperty("visualCatalog");
            visualIdProperty = serializedObject.FindProperty("visualId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "visualCatalog", "visualId");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("船体样貌", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(visualCatalogProperty);

            var catalog = visualCatalogProperty.objectReferenceValue as StripSpriteVisualCatalog;
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
                var nextIndex = EditorGUILayout.Popup("Visual Id", currentIndex, labels.ToArray());
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
        }
    }
}
#endif
