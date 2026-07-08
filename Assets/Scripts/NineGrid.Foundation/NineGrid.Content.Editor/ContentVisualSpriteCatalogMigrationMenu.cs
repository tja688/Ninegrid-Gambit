using System;
using System.Collections.Generic;
using System.IO;
using Luban.SimpleJSON;
using NineGrid.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 一次性：从旧 visual_asset / content_visual JSON 灌图进 Catalog SO。
    /// </summary>
    public static class ContentVisualSpriteCatalogMigrationMenu
    {
        private const string MenuPath = "TableNine/Content/Migrate Visual Assets Into Catalog SOs";
        private const string AssetFolder = "Assets/Arts/ContentVisual";
        private const string LubanRelative = "Assets/StreamingAssets/TableNine/LubanData";

        [MenuItem(MenuPath)]
        public static void Migrate()
        {
            try
            {
                var report = RunMigration();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "[ContentVisual] Catalog SO migration: entries=" + report.EntryCount
                    + " icons=" + report.IconAssigned
                    + " faces=" + report.FaceAssigned
                    + " missingIcon=" + report.MissingIcon
                    + " missingFace=" + report.MissingFace);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public static MigrationReport RunMigration()
        {
            EnsureFolder(AssetFolder);

            // Always recreate catalogs so script refs match standalone SO files.
            foreach (var path in new[]
                     {
                         AssetFolder + "/HelpCardVisualCatalog.asset",
                         AssetFolder + "/MonsterVisualCatalog.asset",
                         AssetFolder + "/RelicVisualCatalog.asset",
                         AssetFolder + "/SkillVisualCatalog.asset",
                         AssetFolder + "/MiscVisualCatalog.asset"
                     })
            {
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) != null
                    || File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
            var set = LoadOrCreateCatalogSet();
            var dataDir = Path.Combine(Directory.GetCurrentDirectory(), LubanRelative);
            var contentJson = File.ReadAllText(Path.Combine(dataDir, "tablenine_tbcontentvisual.json"));
            var assetJson = File.ReadAllText(Path.Combine(dataDir, "tablenine_tbvisualasset.json"));

            var assetKeyByVisualId = new Dictionary<string, string>(StringComparer.Ordinal);
            var assetArray = JSON.Parse(assetJson).AsArray;
            for (var i = 0; i < assetArray.Count; i++)
            {
                var node = assetArray[i];
                var visualId = node["visual_id"].Value;
                var assetKey = node["asset_key"].Value;
                if (!string.IsNullOrEmpty(visualId))
                {
                    assetKeyByVisualId[visualId] = assetKey ?? string.Empty;
                }
            }

            var report = new MigrationReport();
            var contentArray = JSON.Parse(contentJson).AsArray;
            for (var i = 0; i < contentArray.Count; i++)
            {
                var node = contentArray[i];
                var contentId = node["content_id"].Value;
                var kindRaw = node["content_kind"].Value;
                ContentVisualKind kind;
                if (!Enum.TryParse(kindRaw, true, out kind))
                {
                    kind = ContentVisualKind.Unknown;
                }

                var catalog = set.ResolveCatalog(kind);
                if (catalog == null || string.IsNullOrEmpty(contentId))
                {
                    continue;
                }

                catalog.EnsureEntry(contentId);
                report.EntryCount++;

                Sprite icon;
                Sprite face;
                catalog.TryGet(contentId, out icon, out face);

                var iconKey = node["icon_key"] != null ? node["icon_key"].Value : string.Empty;
                var faceKey = node["face_key"] != null ? node["face_key"].Value : string.Empty;

                Sprite resolvedIcon;
                if (TryResolveSprite(iconKey, assetKeyByVisualId, out resolvedIcon))
                {
                    icon = resolvedIcon;
                    report.IconAssigned++;
                }
                else if (!string.IsNullOrEmpty(iconKey))
                {
                    report.MissingIcon++;
                }

                Sprite resolvedFace;
                if (TryResolveSprite(faceKey, assetKeyByVisualId, out resolvedFace))
                {
                    face = resolvedFace;
                    report.FaceAssigned++;
                }
                else if (!string.IsNullOrEmpty(faceKey))
                {
                    report.MissingFace++;
                }

                catalog.SetSprites(contentId, icon, face);
                EditorUtility.SetDirty(catalog);
            }

            return report;
        }

        public static ContentVisualSpriteCatalogSet LoadOrCreateCatalogSet()
        {
            EnsureFolder(AssetFolder);
            return new ContentVisualSpriteCatalogSet
            {
                helpCards = LoadOrCreate<HelpCardVisualCatalogSO>(AssetFolder + "/HelpCardVisualCatalog.asset"),
                monsters = LoadOrCreate<MonsterVisualCatalogSO>(AssetFolder + "/MonsterVisualCatalog.asset"),
                relics = LoadOrCreate<RelicVisualCatalogSO>(AssetFolder + "/RelicVisualCatalog.asset"),
                skills = LoadOrCreate<SkillVisualCatalogSO>(AssetFolder + "/SkillVisualCatalog.asset"),
                misc = LoadOrCreate<MiscVisualCatalogSO>(AssetFolder + "/MiscVisualCatalog.asset")
            };
        }

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                EnsureScriptReference(existing);
                return existing;
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, assetPath);
            EnsureScriptReference(created);
            return created;
        }

        private static void EnsureScriptReference(ScriptableObject asset)
        {
            if (asset == null)
            {
                return;
            }

            var monoScript = MonoScript.FromScriptableObject(asset);
            if (monoScript == null)
            {
                return;
            }

            var serialized = new SerializedObject(asset);
            var scriptProperty = serialized.FindProperty("m_Script");
            if (scriptProperty != null && scriptProperty.objectReferenceValue != monoScript)
            {
                scriptProperty.objectReferenceValue = monoScript;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        private static bool TryResolveSprite(
            string visualOrLegacyKey,
            IReadOnlyDictionary<string, string> assetKeyByVisualId,
            out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(visualOrLegacyKey))
            {
                return false;
            }

            if (ContentVisualSpriteKeyCodec.TryDecodeLegacy(visualOrLegacyKey, out sprite) && sprite != null)
            {
                return true;
            }

            string assetKey;
            if (assetKeyByVisualId.TryGetValue(visualOrLegacyKey, out assetKey)
                && !string.IsNullOrEmpty(assetKey)
                && ContentVisualSpriteKeyCodec.TryDecodeLegacy(assetKey, out sprite)
                && sprite != null)
            {
                return true;
            }

            return ContentVisualSpriteKeyCodec.TryDecode(visualOrLegacyKey, out sprite) && sprite != null;
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        public sealed class MigrationReport
        {
            public int EntryCount;
            public int IconAssigned;
            public int FaceAssigned;
            public int MissingIcon;
            public int MissingFace;
        }
    }
}
