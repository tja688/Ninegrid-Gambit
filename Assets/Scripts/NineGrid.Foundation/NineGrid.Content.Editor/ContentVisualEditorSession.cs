using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Content;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public enum ContentVisualKeySlot
    {
        Icon,
        Face
    }

    public sealed class ContentVisualEditorRowState
    {
        public ContentVisualXlsxRow Source { get; set; }
        public Sprite SavedIcon { get; set; }
        public Sprite SavedFace { get; set; }
        public Sprite Icon { get; set; }
        public Sprite Face { get; set; }
        public bool IsChecked { get; set; }

        public string ContentId => Source?.ContentId ?? string.Empty;
        public string ContentKind => Source?.ContentKind ?? string.Empty;
        public string Description => Source?.Description ?? string.Empty;

        public bool IsDirty => Icon != SavedIcon || Face != SavedFace;
        public bool HasIcon => Icon != null;
        public bool HasFace => Face != null;

        public void Revert()
        {
            Icon = SavedIcon;
            Face = SavedFace;
        }

        public void MarkSaved()
        {
            SavedIcon = Icon;
            SavedFace = Face;
        }
    }

    public sealed class CardFrameStyleEditorRowState
    {
        public CardFrameStyleXlsxRow Source { get; set; }
        public Color SavedColor { get; set; } = Color.white;
        public Color Color { get; set; } = Color.white;

        public string StyleId => Source?.StyleId ?? string.Empty;
        public bool IsDirty => SavedColor != Color;

        public void MarkSaved()
        {
            SavedColor = Color;
        }

        public CardFrameStyleXlsxRow ToPatchRow()
        {
            return new CardFrameStyleXlsxRow
            {
                StyleId = StyleId,
                SheetRowIndex = Source?.SheetRowIndex ?? -1,
                ColorR = Color.r,
                ColorG = Color.g,
                ColorB = Color.b,
                ColorA = Color.a
            };
        }
    }

    public sealed class ContentVisualEditorSession
    {
        public const string CatalogAssetFolder = "Assets/Arts/ContentVisual";

        private const string PrefKindFilter = "NineGrid.ContentVisualEditor.KindFilter";
        private const string PrefSearch = "NineGrid.ContentVisualEditor.Search";
        private const string PrefMissingIcon = "NineGrid.ContentVisualEditor.MissingIcon";
        private const string PrefMissingFace = "NineGrid.ContentVisualEditor.MissingFace";
        private const string PrefDirtyOnly = "NineGrid.ContentVisualEditor.DirtyOnly";
        private const string PrefActiveTab = "NineGrid.ContentVisualEditor.ActiveTab";

        private readonly List<ContentVisualEditorRowState> rows = new();
        private readonly List<CardFrameStyleEditorRowState> frameStyleRows = new();
        private string focusedContentId = string.Empty;

        public IReadOnlyList<ContentVisualEditorRowState> Rows => rows;
        public IReadOnlyList<CardFrameStyleEditorRowState> FrameStyleRows => frameStyleRows;
        public GameContentCatalog CoreCatalog { get; private set; }
        public ContentVisualCatalog VisualCatalog { get; private set; }
        public CardFrameStyleCatalog FrameStyleCatalog { get; private set; }
        public ContentVisualSpriteCatalogSet SpriteCatalogs { get; private set; }
        public string XlsxPath { get; private set; }
        public string FrameStyleXlsxPath { get; private set; }
        public string KindFilter { get; set; } = "All";
        public string SearchText { get; set; } = string.Empty;
        public bool FilterMissingIcon { get; set; }
        public bool FilterMissingFace { get; set; }
        public bool FilterDirtyOnly { get; set; }
        public int ActiveTab { get; set; }

        public string FocusedContentId
        {
            get => focusedContentId;
            set => focusedContentId = value ?? string.Empty;
        }

        public int DirtyCount =>
            rows.Count(row => row.IsDirty) + frameStyleRows.Count(row => row.IsDirty);

        public int MissingIconCount => rows.Count(row => !row.HasIcon);
        public int MissingFaceCount => rows.Count(row => !row.HasFace);

        public void LoadPreferences()
        {
            KindFilter = EditorPrefs.GetString(PrefKindFilter, "All");
            SearchText = EditorPrefs.GetString(PrefSearch, string.Empty);
            FilterMissingIcon = EditorPrefs.GetBool(PrefMissingIcon, false);
            FilterMissingFace = EditorPrefs.GetBool(PrefMissingFace, false);
            FilterDirtyOnly = EditorPrefs.GetBool(PrefDirtyOnly, false);
            ActiveTab = EditorPrefs.GetInt(PrefActiveTab, 0);
        }

        public void SavePreferences()
        {
            EditorPrefs.SetString(PrefKindFilter, KindFilter ?? "All");
            EditorPrefs.SetString(PrefSearch, SearchText ?? string.Empty);
            EditorPrefs.SetBool(PrefMissingIcon, FilterMissingIcon);
            EditorPrefs.SetBool(PrefMissingFace, FilterMissingFace);
            EditorPrefs.SetBool(PrefDirtyOnly, FilterDirtyOnly);
            EditorPrefs.SetInt(PrefActiveTab, ActiveTab);
        }

        public void ReloadFromXlsx()
        {
            XlsxPath = ContentVisualXlsxIO.ResolveAbsolutePath();
            FrameStyleXlsxPath = CardFrameStyleXlsxIO.ResolveAbsolutePath();
            SpriteCatalogs = LoadOrCreateCatalogSet();

            var xlsxRows = ContentVisualXlsxIO.ReadAll(XlsxPath);
            rows.Clear();
            for (var i = 0; i < xlsxRows.Count; i++)
            {
                var source = xlsxRows[i];
                ContentVisualKind kind;
                if (!Enum.TryParse(source.ContentKind, true, out kind))
                {
                    kind = ContentVisualKind.Unknown;
                }

                Sprite icon = null;
                Sprite face = null;
                var catalog = SpriteCatalogs.ResolveCatalog(kind);
                if (catalog != null)
                {
                    catalog.EnsureEntry(source.ContentId);
                    catalog.TryGet(source.ContentId, out icon, out face);
                }

                var state = new ContentVisualEditorRowState
                {
                    Source = source,
                    Icon = icon,
                    Face = face
                };
                state.MarkSaved();
                rows.Add(state);
            }

            frameStyleRows.Clear();
            var frameRows = CardFrameStyleXlsxIO.ReadAll(FrameStyleXlsxPath);
            for (var i = 0; i < frameRows.Count; i++)
            {
                var source = frameRows[i];
                var state = new CardFrameStyleEditorRowState
                {
                    Source = source,
                    Color = new Color(source.ColorR, source.ColorG, source.ColorB, source.ColorA)
                };
                state.MarkSaved();
                frameStyleRows.Add(state);
            }

            ReloadCatalogs();

            if (!string.IsNullOrEmpty(focusedContentId) && !rows.Any(row => row.ContentId == focusedContentId))
            {
                focusedContentId = string.Empty;
            }
        }

        public void ReloadCatalogs()
        {
            var dataDirectory = ContentVisualBootstrap.ResolveLubanDataDirectory();
            CoreCatalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
            VisualCatalog = TableNineVisualCatalogFactory.CreateFromDirectory(dataDirectory);
            FrameStyleCatalog = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(dataDirectory);
        }

        public IEnumerable<ContentVisualEditorRowState> GetFilteredRows()
        {
            var search = (SearchText ?? string.Empty).Trim();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!PassesKindFilter(row))
                {
                    continue;
                }

                if (FilterMissingIcon && row.HasIcon)
                {
                    continue;
                }

                if (FilterMissingFace && row.HasFace)
                {
                    continue;
                }

                if (FilterDirtyOnly && !row.IsDirty)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(search) && !MatchesSearch(row, search))
                {
                    continue;
                }

                yield return row;
            }
        }

        public ContentVisualEditorRowState GetFocusedRow()
        {
            if (string.IsNullOrEmpty(focusedContentId))
            {
                return null;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].ContentId == focusedContentId)
                {
                    return rows[i];
                }
            }

            return null;
        }

        public List<ContentVisualEditorRowState> GetCheckedRows()
        {
            return rows.Where(row => row.IsChecked).ToList();
        }

        public void SelectAllFiltered()
        {
            var ids = new HashSet<string>(GetFilteredRows().Select(row => row.ContentId));
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].IsChecked = ids.Contains(rows[i].ContentId);
            }
        }

        public void DeselectAll()
        {
            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].IsChecked = false;
            }
        }

        public void ApplySpriteToRows(
            IEnumerable<ContentVisualEditorRowState> targets,
            ContentVisualKeySlot slot,
            Sprite sprite)
        {
            foreach (var row in targets)
            {
                if (slot == ContentVisualKeySlot.Icon)
                {
                    row.Icon = sprite;
                }
                else
                {
                    row.Face = sprite;
                }
            }
        }

        public void ClearKeyOnRows(IEnumerable<ContentVisualEditorRowState> targets, ContentVisualKeySlot slot)
        {
            foreach (var row in targets)
            {
                if (slot == ContentVisualKeySlot.Icon)
                {
                    row.Icon = null;
                }
                else
                {
                    row.Face = null;
                }
            }
        }

        public bool TrySave(out string error)
        {
            error = null;
            if (!ContentVisualXlsxIO.CanWrite(FrameStyleXlsxPath, out error))
            {
                return false;
            }

            var dirtyRows = rows.Where(row => row.IsDirty).ToList();
            var dirtyFrameRows = frameStyleRows.Where(row => row.IsDirty).Select(row => row.ToPatchRow()).ToList();
            if (dirtyRows.Count == 0 && dirtyFrameRows.Count == 0)
            {
                return true;
            }

            try
            {
                for (var i = 0; i < dirtyRows.Count; i++)
                {
                    var row = dirtyRows[i];
                    ContentVisualKind kind;
                    if (!Enum.TryParse(row.ContentKind, true, out kind))
                    {
                        kind = ContentVisualKind.Unknown;
                    }

                    var catalog = SpriteCatalogs.ResolveCatalog(kind);
                    if (catalog == null)
                    {
                        continue;
                    }

                    catalog.SetSprites(row.ContentId, row.Icon, row.Face);
                    EditorUtility.SetDirty(catalog);
                    row.MarkSaved();
                }

                if (dirtyRows.Count > 0)
                {
                    AssetDatabase.SaveAssets();
                }

                if (dirtyFrameRows.Count > 0)
                {
                    CardFrameStyleXlsxIO.PatchColors(FrameStyleXlsxPath, dirtyFrameRows);
                    for (var i = 0; i < frameStyleRows.Count; i++)
                    {
                        if (frameStyleRows[i].IsDirty)
                        {
                            frameStyleRows[i].MarkSaved();
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryResolve(string contentId, out ContentVisualResolvedView view)
        {
            view = null;
            if (CoreCatalog == null || VisualCatalog == null || FrameStyleCatalog == null)
            {
                return false;
            }

            return ContentVisualResolver.TryResolve(
                contentId,
                CoreCatalog,
                VisualCatalog,
                FrameStyleCatalog,
                SpriteCatalogs,
                out view);
        }

        public bool TryResolveRow(ContentVisualEditorRowState row, out ContentVisualResolvedView view)
        {
            view = null;
            if (row == null || CoreCatalog == null)
            {
                return false;
            }

            ContentVisualKind kind;
            if (!Enum.TryParse(row.ContentKind, true, out kind))
            {
                kind = ContentVisualKind.Unknown;
            }

            var tempCatalog = new ContentVisualCatalog();
            tempCatalog.Add(new ContentVisualDefinition(
                row.ContentId,
                kind,
                row.Description));

            var provider = new SessionSpriteOverrideProvider(SpriteCatalogs, row);
            var frameCatalog = BuildSessionFrameStyleCatalog();
            return ContentVisualResolver.TryResolve(
                row.ContentId,
                CoreCatalog,
                tempCatalog,
                frameCatalog,
                provider,
                out view);
        }

        public CardFrameStyleCatalog BuildSessionFrameStyleCatalog()
        {
            var catalog = new CardFrameStyleCatalog();
            for (var i = 0; i < frameStyleRows.Count; i++)
            {
                var row = frameStyleRows[i];
                catalog.Add(new CardFrameStyleDefinition(row.StyleId, ToContentColor(row.Color)));
            }

            return catalog;
        }

        public string GetDisplayName(string contentId, string contentKind)
        {
            ContentVisualResolvedView view;
            if (TryResolve(contentId, out view) && !string.IsNullOrEmpty(view.DisplayName))
            {
                return view.DisplayName;
            }

            return contentId;
        }

        public ContentVisualSpriteCatalogSO ResolveCatalogForKindFilter(string kindFilter)
        {
            if (SpriteCatalogs == null
                || string.IsNullOrEmpty(kindFilter)
                || kindFilter == "All")
            {
                return null;
            }

            ContentVisualKind kind;
            if (!Enum.TryParse(kindFilter, true, out kind))
            {
                return null;
            }

            return SpriteCatalogs.ResolveCatalog(kind);
        }

        /// <summary>
        /// 一次性把类型 DefaultFace 写入该类型全部会话行的 face（初始化，非持续覆盖）。
        /// </summary>
        public int ApplyKindDefaultFaceToSession(string kindFilter)
        {
            var catalog = ResolveCatalogForKindFilter(kindFilter);
            if (catalog == null || catalog.DefaultFace == null)
            {
                return 0;
            }

            var face = catalog.DefaultFace;
            var count = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!string.Equals(row.ContentKind, kindFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                row.Face = face;
                count++;
            }

            return count;
        }

        public bool TryPersistKindDefaults(string kindFilter, Sprite defaultFace, Sprite fallbackIcon, out string error)
        {
            error = null;
            var catalog = ResolveCatalogForKindFilter(kindFilter);
            if (catalog == null)
            {
                error = "当前类型没有对应 Catalog SO。";
                return false;
            }

            catalog.DefaultFace = defaultFace;
            catalog.FallbackIcon = fallbackIcon;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// 导出全部卡图关联到语义化 JSON（便于日后分析还原，非强类型容器）。
        /// </summary>
        public bool TryExportCardSpriteManifest(string assetFolder, out string absolutePath, out string error)
        {
            absolutePath = null;
            error = null;
            if (SpriteCatalogs == null)
            {
                error = "Catalog SO 未加载。";
                return false;
            }

            EnsureFolder(assetFolder);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var relativePath = assetFolder.TrimEnd('/') + "/card-sprite-manifest-" + stamp + ".json";
            absolutePath = Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                var sb = new StringBuilder(8192);
                sb.AppendLine("{");
                sb.AppendLine("  \"schema\": \"table-nine.card-sprite-manifest.v1\",");
                sb.AppendLine("  \"exportedAt\": \"" + DateTime.Now.ToString("o") + "\",");
                sb.AppendLine("  \"note\": \"语义化卡图关联快照：用 contentId/displayName/kind 识别卡，而非内部编码。后期容器改版可用分析方式还原。\",");
                sb.AppendLine("  \"entries\": [");

                var first = true;
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    ContentVisualKind kind;
                    if (!Enum.TryParse(row.ContentKind, true, out kind))
                    {
                        kind = ContentVisualKind.Unknown;
                    }

                    var catalog = SpriteCatalogs.ResolveCatalog(kind);
                    Sprite icon = row.Icon;
                    Sprite face = row.Face;
                    if (catalog != null)
                    {
                        Sprite catalogIcon;
                        Sprite catalogFace;
                        if (catalog.TryGet(row.ContentId, out catalogIcon, out catalogFace))
                        {
                            if (icon == null)
                            {
                                icon = catalogIcon;
                            }

                            if (face == null)
                            {
                                face = catalogFace;
                            }
                        }
                    }

                    if (icon == null && face == null)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.AppendLine(",");
                    }

                    first = false;
                    var displayName = GetDisplayName(row.ContentId, row.ContentKind);
                    sb.AppendLine("    {");
                    sb.AppendLine("      \"contentId\": " + JsonString(row.ContentId) + ",");
                    sb.AppendLine("      \"displayName\": " + JsonString(displayName) + ",");
                    sb.AppendLine("      \"contentKind\": " + JsonString(row.ContentKind) + ",");
                    sb.AppendLine("      \"description\": " + JsonString(row.Description) + ",");
                    sb.AppendLine("      \"icon\": " + SpriteRefJson(icon) + ",");
                    sb.Append("      \"face\": " + SpriteRefJson(face));
                    sb.AppendLine();
                    sb.Append("    }");
                }

                sb.AppendLine();
                sb.AppendLine("  ],");
                sb.AppendLine("  \"kindDefaults\": [");
                AppendKindDefaultJson(sb, "HelpCard", SpriteCatalogs.helpCards, true);
                AppendKindDefaultJson(sb, "Monster", SpriteCatalogs.monsters, false);
                AppendKindDefaultJson(sb, "Relic", SpriteCatalogs.relics, false);
                AppendKindDefaultJson(sb, "Skill", SpriteCatalogs.skills, false);
                AppendKindDefaultJson(sb, "Misc(Avatar/Room/MonsterDeck)", SpriteCatalogs.misc, false);
                AppendKindDefaultJson(sb, "ChoiceOption", SpriteCatalogs.choiceOptions, false);
                sb.AppendLine();
                sb.AppendLine("  ]");
                sb.AppendLine("}");

                File.WriteAllText(absolutePath, sb.ToString(), Encoding.UTF8);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void AppendKindDefaultJson(
            StringBuilder sb,
            string label,
            ContentVisualSpriteCatalogSO catalog,
            bool first)
        {
            if (!first)
            {
                sb.AppendLine(",");
            }

            sb.AppendLine("    {");
            sb.AppendLine("      \"kind\": " + JsonString(label) + ",");
            if (catalog == null)
            {
                sb.AppendLine("      \"defaultFace\": null,");
                sb.AppendLine("      \"fallbackIcon\": null");
            }
            else
            {
                sb.AppendLine("      \"defaultFace\": " + SpriteRefJson(catalog.DefaultFace) + ",");
                sb.AppendLine("      \"fallbackIcon\": " + SpriteRefJson(catalog.FallbackIcon));
            }

            sb.Append("    }");
        }

        private static string SpriteRefJson(Sprite sprite)
        {
            if (sprite == null)
            {
                return "null";
            }

            var path = AssetDatabase.GetAssetPath(sprite) ?? string.Empty;
            var guid = string.IsNullOrEmpty(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            return "{"
                   + "\"name\":" + JsonString(sprite.name)
                   + ",\"assetPath\":" + JsonString(path)
                   + ",\"guid\":" + JsonString(guid)
                   + "}";
        }

        private static string JsonString(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t") + "\"";
        }

        public static ContentVisualSpriteCatalogSet LoadOrCreateCatalogSet()
        {
            EnsureFolder(CatalogAssetFolder);
            return new ContentVisualSpriteCatalogSet
            {
                helpCards = LoadOrCreate<HelpCardVisualCatalogSO>(CatalogAssetFolder + "/HelpCardVisualCatalog.asset"),
                monsters = LoadOrCreate<MonsterVisualCatalogSO>(CatalogAssetFolder + "/MonsterVisualCatalog.asset"),
                relics = LoadOrCreate<RelicVisualCatalogSO>(CatalogAssetFolder + "/RelicVisualCatalog.asset"),
                skills = LoadOrCreate<SkillVisualCatalogSO>(CatalogAssetFolder + "/SkillVisualCatalog.asset"),
                misc = LoadOrCreate<MiscVisualCatalogSO>(CatalogAssetFolder + "/MiscVisualCatalog.asset"),
                choiceOptions = LoadOrCreate<ChoiceOptionVisualCatalogSO>(
                    CatalogAssetFolder + "/ChoiceOptionVisualCatalog.asset")
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

        private bool PassesKindFilter(ContentVisualEditorRowState row)
        {
            if (string.IsNullOrEmpty(KindFilter) || KindFilter == "All")
            {
                return true;
            }

            return string.Equals(row.ContentKind, KindFilter, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesSearch(ContentVisualEditorRowState row, string search)
        {
            if (row.ContentId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (row.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var displayName = GetDisplayName(row.ContentId, row.ContentKind);
            return displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ContentColor ToContentColor(Color color)
        {
            return new ContentColor(color.r, color.g, color.b, color.a);
        }

        private sealed class SessionSpriteOverrideProvider : IContentVisualSpriteProvider
        {
            private readonly ContentVisualSpriteCatalogSet mFallback;
            private readonly ContentVisualEditorRowState mOverride;

            public SessionSpriteOverrideProvider(
                ContentVisualSpriteCatalogSet fallback,
                ContentVisualEditorRowState rowOverride)
            {
                mFallback = fallback;
                mOverride = rowOverride;
            }

            public bool TryGet(ContentVisualKind kind, string contentId, out Sprite icon, out Sprite face)
            {
                if (mOverride != null
                    && string.Equals(mOverride.ContentId, contentId, StringComparison.Ordinal))
                {
                    icon = mOverride.Icon;
                    face = mOverride.Face;
                    return icon != null || face != null;
                }

                if (mFallback != null)
                {
                    return mFallback.TryGet(kind, contentId, out icon, out face);
                }

                icon = null;
                face = null;
                return false;
            }
        }
    }
}
