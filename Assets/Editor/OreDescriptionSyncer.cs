#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NineGrid.Data;
using NineGrid.GameFlow;

namespace NineGrid.Editor
{
    /// <summary>
    /// 将 OreMechanicalText 生成的机制描述同步到矿石 SO 与 OreCatalog。
    /// </summary>
    public static class OreDescriptionSyncer
    {
        const string OreDir = "Assets/ScriptableObjects/Data/Ores";
        const string CatalogPath = "Assets/ScriptableObjects/Data/OreCatalog.asset";

        [MenuItem("NineGrid/Tools/Sync Ore Mechanical Descriptions")]
        public static void Sync()
        {
            var count = 0;
            var guids = AssetDatabase.FindAssets("t:OreDataSO", new[] { OreDir });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<OreDataSO>(path);
                if (so == null)
                {
                    continue;
                }

                var text = OreMechanicalText.Build(so.OreId, so.DisplayName, so.BasePoints, so.Traits);
                var soField = typeof(OreDataSO).GetField("description",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                soField?.SetValue(so, text);
                EditorUtility.SetDirty(so);
                count++;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<OreCatalog>(CatalogPath);
            if (catalog != null)
            {
                var entriesField = typeof(OreCatalog).GetField("entries",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var entries = entriesField?.GetValue(catalog) as System.Collections.Generic.List<OreDataEntry>;
                if (entries != null)
                {
                    for (var i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        var text = OreMechanicalText.Build(entry.OreId, entry.DisplayName, entry.BasePoints, entry.Traits);
                        var descField = typeof(OreDataEntry).GetField("description",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        descField?.SetValue(entry, text);
                    }

                    EditorUtility.SetDirty(catalog);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[OreDescriptionSyncer] 已同步 {count} 个矿石 SO 与 OreCatalog。");
        }
    }
}
#endif
