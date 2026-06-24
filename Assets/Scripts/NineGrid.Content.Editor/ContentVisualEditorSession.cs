using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Presentation.Visuals;
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
        public string SavedFaceKey { get; set; } = string.Empty;
        public string SavedIconKey { get; set; } = string.Empty;
        public string FaceKey { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool IsChecked { get; set; }

        public string ContentId => Source?.ContentId ?? string.Empty;
        public string ContentKind => Source?.ContentKind ?? string.Empty;
        public string Description => Source?.Description ?? string.Empty;

        public bool IsDirty =>
            !string.Equals(FaceKey, SavedFaceKey, StringComparison.Ordinal)
            || !string.Equals(IconKey, SavedIconKey, StringComparison.Ordinal);

        public bool HasIconKey => !string.IsNullOrEmpty(IconKey);
        public bool HasFaceKey => !string.IsNullOrEmpty(FaceKey);

        public void Revert()
        {
            FaceKey = SavedFaceKey;
            IconKey = SavedIconKey;
        }

        public void MarkSaved()
        {
            SavedFaceKey = FaceKey ?? string.Empty;
            SavedIconKey = IconKey ?? string.Empty;
        }

        public ContentVisualXlsxRow ToPatchRow()
        {
            return new ContentVisualXlsxRow
            {
                ContentId = ContentId,
                SheetRowIndex = Source.SheetRowIndex,
                FaceKey = FaceKey ?? string.Empty,
                IconKey = IconKey ?? string.Empty
            };
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
        public VisualAssetCatalog VisualAssetCatalog { get; private set; }
        public CardFrameStyleCatalog FrameStyleCatalog { get; private set; }
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
        public int MissingIconCount => rows.Count(row => !row.HasIconKey);
        public int MissingFaceCount => rows.Count(row => !row.HasFaceKey);

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
            var xlsxRows = ContentVisualXlsxIO.ReadAll(XlsxPath);
            rows.Clear();

            for (var i = 0; i < xlsxRows.Count; i++)
            {
                var source = xlsxRows[i];
                var state = new ContentVisualEditorRowState
                {
                    Source = source,
                    FaceKey = source.FaceKey ?? string.Empty,
                    IconKey = source.IconKey ?? string.Empty
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
            VisualAssetCatalog = TableNineVisualAssetCatalogFactory.CreateFromDirectory(dataDirectory);
            FrameStyleCatalog = TableNineCardFrameStyleCatalogFactory.CreateFromDirectory(dataDirectory);
            ContentVisualSpriteLoader.Configure(VisualAssetCatalog);
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

                if (FilterMissingIcon && row.HasIconKey)
                {
                    continue;
                }

                if (FilterMissingFace && row.HasFaceKey)
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
                ContentVisualKind kind;
                if (!Enum.TryParse(row.ContentKind, true, out kind))
                {
                    kind = ContentVisualKind.Unknown;
                }

                var key = ContentVisualSpriteKeyCodec.Encode(slot, row.ContentId, kind, sprite);
                if (slot == ContentVisualKeySlot.Icon)
                {
                    row.IconKey = key;
                }
                else
                {
                    row.FaceKey = key;
                }
            }
        }

        public void ClearKeyOnRows(IEnumerable<ContentVisualEditorRowState> targets, ContentVisualKeySlot slot)
        {
            foreach (var row in targets)
            {
                if (slot == ContentVisualKeySlot.Icon)
                {
                    row.IconKey = string.Empty;
                }
                else
                {
                    row.FaceKey = string.Empty;
                }
            }
        }

        public bool TrySave(out string error)
        {
            error = null;
            if (!ContentVisualXlsxIO.CanWrite(XlsxPath, out error))
            {
                return false;
            }

            if (!ContentVisualXlsxIO.CanWrite(FrameStyleXlsxPath, out error))
            {
                return false;
            }

            var dirtyRows = rows.Where(row => row.IsDirty).Select(row => row.ToPatchRow()).ToList();
            var dirtyFrameRows = frameStyleRows.Where(row => row.IsDirty).Select(row => row.ToPatchRow()).ToList();
            if (dirtyRows.Count == 0 && dirtyFrameRows.Count == 0)
            {
                return true;
            }

            try
            {
                if (dirtyRows.Count > 0)
                {
                    ContentVisualXlsxIO.PatchVisualKeys(XlsxPath, dirtyRows);
                    var assetUpserts = BuildAssetUpsertsForDirtyRows();
                    if (assetUpserts.Count > 0)
                    {
                        VisualAssetXlsxIO.UpsertRows(VisualAssetXlsxIO.ResolveAbsolutePath(), assetUpserts);
                    }

                    for (var i = 0; i < rows.Count; i++)
                    {
                        if (rows[i].IsDirty)
                        {
                            rows[i].MarkSaved();
                        }
                    }
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
                row.Description,
                row.FaceKey,
                string.Empty,
                row.IconKey));

            var frameCatalog = BuildSessionFrameStyleCatalog();
            return ContentVisualResolver.TryResolve(
                row.ContentId,
                CoreCatalog,
                tempCatalog,
                frameCatalog,
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

        private List<VisualAssetXlsxRow> BuildAssetUpsertsForDirtyRows()
        {
            var upserts = new List<VisualAssetXlsxRow>();
            var seen = new HashSet<string>();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.IsDirty)
                {
                    continue;
                }

                ContentVisualKind kind;
                if (!Enum.TryParse(row.ContentKind, true, out kind))
                {
                    kind = ContentVisualKind.Unknown;
                }

                TryAddAssetUpsert(upserts, seen, row, ContentVisualKeySlot.Icon, kind);
                TryAddAssetUpsert(upserts, seen, row, ContentVisualKeySlot.Face, kind);
            }

            return upserts;
        }

        private static void TryAddAssetUpsert(
            List<VisualAssetXlsxRow> upserts,
            HashSet<string> seen,
            ContentVisualEditorRowState row,
            ContentVisualKeySlot slot,
            ContentVisualKind kind)
        {
            var key = slot == ContentVisualKeySlot.Icon ? row.IconKey : row.FaceKey;
            if (string.IsNullOrEmpty(key) || !VisualIdNaming.IsVisualId(key) || !seen.Add(key))
            {
                return;
            }

            Sprite sprite;
            if (!ContentVisualSpriteKeyCodec.TryDecode(key, out sprite) && !ContentVisualSpriteKeyCodec.TryDecodeLegacy(key, out sprite))
            {
                upserts.Add(new VisualAssetXlsxRow
                {
                    VisualId = key,
                    Kind = "sprite",
                    AssetKey = slot == ContentVisualKeySlot.Icon
                        ? VisualAssetKeyNaming.FromConvention(kind, VisualAssetSlot.Icon, row.ContentId)
                        : VisualAssetKeyNaming.FromConvention(kind, VisualAssetSlot.Face, row.ContentId)
                });
                return;
            }

            var upsert = ContentVisualSpriteKeyCodec.BuildAssetUpsert(slot, row.ContentId, kind, sprite);
            if (upsert != null)
            {
                upserts.Add(upsert);
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

            if (row.FaceKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || row.IconKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
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
    }
}
