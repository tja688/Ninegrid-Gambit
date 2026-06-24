using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content;
using NineGrid.Core.Content;
using UnityEditor;

namespace NineGrid.Content.Editor
{
    public enum ContentVisualKeySlot
    {
        Icon,
        Face,
        Frame
    }

    public sealed class ContentVisualEditorRowState
    {
        public ContentVisualXlsxRow Source { get; set; }
        public string SavedFaceKey { get; set; } = string.Empty;
        public string SavedFrameKey { get; set; } = string.Empty;
        public string SavedIconKey { get; set; } = string.Empty;
        public string FaceKey { get; set; } = string.Empty;
        public string FrameKey { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool IsChecked { get; set; }

        public string ContentId => Source?.ContentId ?? string.Empty;
        public string ContentKind => Source?.ContentKind ?? string.Empty;
        public string Description => Source?.Description ?? string.Empty;

        public bool IsDirty =>
            !string.Equals(FaceKey, SavedFaceKey, StringComparison.Ordinal)
            || !string.Equals(FrameKey, SavedFrameKey, StringComparison.Ordinal)
            || !string.Equals(IconKey, SavedIconKey, StringComparison.Ordinal);

        public bool HasIconKey => !string.IsNullOrEmpty(IconKey);
        public bool HasFaceKey => !string.IsNullOrEmpty(FaceKey);
        public bool HasFrameKey => !string.IsNullOrEmpty(FrameKey);

        public void Revert()
        {
            FaceKey = SavedFaceKey;
            FrameKey = SavedFrameKey;
            IconKey = SavedIconKey;
        }

        public void MarkSaved()
        {
            SavedFaceKey = FaceKey ?? string.Empty;
            SavedFrameKey = FrameKey ?? string.Empty;
            SavedIconKey = IconKey ?? string.Empty;
        }

        public ContentVisualXlsxRow ToPatchRow()
        {
            return new ContentVisualXlsxRow
            {
                ContentId = ContentId,
                SheetRowIndex = Source.SheetRowIndex,
                FaceKey = FaceKey ?? string.Empty,
                FrameKey = FrameKey ?? string.Empty,
                IconKey = IconKey ?? string.Empty
            };
        }
    }

    public sealed class ContentVisualEditorSession
    {
        private const string PrefKindFilter = "NineGrid.ContentVisualEditor.KindFilter";
        private const string PrefSearch = "NineGrid.ContentVisualEditor.Search";
        private const string PrefMissingIcon = "NineGrid.ContentVisualEditor.MissingIcon";
        private const string PrefMissingFace = "NineGrid.ContentVisualEditor.MissingFace";
        private const string PrefMissingFrame = "NineGrid.ContentVisualEditor.MissingFrame";
        private const string PrefDirtyOnly = "NineGrid.ContentVisualEditor.DirtyOnly";

        private readonly List<ContentVisualEditorRowState> rows = new();
        private string focusedContentId = string.Empty;

        public IReadOnlyList<ContentVisualEditorRowState> Rows => rows;
        public GameContentCatalog CoreCatalog { get; private set; }
        public ContentVisualCatalog VisualCatalog { get; private set; }
        public string XlsxPath { get; private set; }
        public string KindFilter { get; set; } = "All";
        public string SearchText { get; set; } = string.Empty;
        public bool FilterMissingIcon { get; set; }
        public bool FilterMissingFace { get; set; }
        public bool FilterMissingFrame { get; set; }
        public bool FilterDirtyOnly { get; set; }
        public string FocusedContentId
        {
            get => focusedContentId;
            set => focusedContentId = value ?? string.Empty;
        }

        public int DirtyCount => rows.Count(row => row.IsDirty);
        public int MissingIconCount => rows.Count(row => !row.HasIconKey);
        public int MissingFaceCount => rows.Count(row => !row.HasFaceKey);
        public int MissingFrameCount => rows.Count(row => !row.HasFrameKey);

        public void LoadPreferences()
        {
            KindFilter = EditorPrefs.GetString(PrefKindFilter, "All");
            SearchText = EditorPrefs.GetString(PrefSearch, string.Empty);
            FilterMissingIcon = EditorPrefs.GetBool(PrefMissingIcon, false);
            FilterMissingFace = EditorPrefs.GetBool(PrefMissingFace, false);
            FilterMissingFrame = EditorPrefs.GetBool(PrefMissingFrame, false);
            FilterDirtyOnly = EditorPrefs.GetBool(PrefDirtyOnly, false);
        }

        public void SavePreferences()
        {
            EditorPrefs.SetString(PrefKindFilter, KindFilter ?? "All");
            EditorPrefs.SetString(PrefSearch, SearchText ?? string.Empty);
            EditorPrefs.SetBool(PrefMissingIcon, FilterMissingIcon);
            EditorPrefs.SetBool(PrefMissingFace, FilterMissingFace);
            EditorPrefs.SetBool(PrefMissingFrame, FilterMissingFrame);
            EditorPrefs.SetBool(PrefDirtyOnly, FilterDirtyOnly);
        }

        public void ReloadFromXlsx()
        {
            XlsxPath = ContentVisualXlsxIO.ResolveAbsolutePath();
            var xlsxRows = ContentVisualXlsxIO.ReadAll(XlsxPath);
            rows.Clear();

            for (var i = 0; i < xlsxRows.Count; i++)
            {
                var source = xlsxRows[i];
                var state = new ContentVisualEditorRowState
                {
                    Source = source,
                    FaceKey = source.FaceKey ?? string.Empty,
                    FrameKey = source.FrameKey ?? string.Empty,
                    IconKey = source.IconKey ?? string.Empty
                };
                state.MarkSaved();
                rows.Add(state);
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

                if (FilterMissingFrame && row.HasFrameKey)
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

        public void ApplyKeyToRows(IEnumerable<ContentVisualEditorRowState> targets, ContentVisualKeySlot slot, string key)
        {
            var normalized = key ?? string.Empty;
            foreach (var row in targets)
            {
                switch (slot)
                {
                    case ContentVisualKeySlot.Icon:
                        row.IconKey = normalized;
                        break;
                    case ContentVisualKeySlot.Face:
                        row.FaceKey = normalized;
                        break;
                    case ContentVisualKeySlot.Frame:
                        row.FrameKey = normalized;
                        break;
                }
            }
        }

        public void ClearKeyOnRows(IEnumerable<ContentVisualEditorRowState> targets, ContentVisualKeySlot slot)
        {
            ApplyKeyToRows(targets, slot, string.Empty);
        }

        public bool TrySave(out string error)
        {
            error = null;
            if (!ContentVisualXlsxIO.CanWrite(XlsxPath, out error))
            {
                return false;
            }

            var dirtyRows = rows.Where(row => row.IsDirty).Select(row => row.ToPatchRow()).ToList();
            if (dirtyRows.Count == 0)
            {
                return true;
            }

            try
            {
                ContentVisualXlsxIO.PatchVisualKeys(XlsxPath, dirtyRows);
                for (var i = 0; i < rows.Count; i++)
                {
                    if (rows[i].IsDirty)
                    {
                        rows[i].MarkSaved();
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
            if (CoreCatalog == null || VisualCatalog == null)
            {
                return false;
            }

            return ContentVisualResolver.TryResolve(contentId, CoreCatalog, VisualCatalog, out view);
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
                || row.FrameKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || row.IconKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var displayName = GetDisplayName(row.ContentId, row.ContentKind);
            return displayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
