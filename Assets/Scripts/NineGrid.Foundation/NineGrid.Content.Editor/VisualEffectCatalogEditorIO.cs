#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Content.CardPresentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 特效库表编辑读写（Authoring Arts + StreamingAssets 同步）与目录扫描生成。
    /// </summary>
    public static class VisualEffectCatalogEditorIO
    {
        public sealed class EditorRow
        {
            public VisualEffectEntryDto Dto { get; set; }
            public string SavedFingerprint { get; set; } = string.Empty;

            public string Id => Dto?.id ?? string.Empty;

            public bool IsDirty =>
                !string.Equals(ComputeFingerprint(), SavedFingerprint ?? string.Empty, StringComparison.Ordinal);

            public void MarkSaved()
            {
                SavedFingerprint = ComputeFingerprint();
            }

            public string ComputeFingerprint()
            {
                var d = Dto;
                if (d == null)
                {
                    return string.Empty;
                }

                return string.Join("|",
                    d.id ?? string.Empty,
                    d.category ?? string.Empty,
                    d.variantId ?? string.Empty,
                    d.size ?? string.Empty,
                    d.color ?? string.Empty,
                    d.sheetPath ?? string.Empty,
                    d.defaultFps.ToString("R"),
                    d.defaultScale.ToString("R"),
                    d.displayName ?? string.Empty,
                    TimingFingerprint(d.timingBindings));
            }

            private static string TimingFingerprint(string[] bindings)
            {
                if (bindings == null || bindings.Length == 0)
                {
                    return string.Empty;
                }

                return string.Join(",", bindings);
            }
        }

        [Serializable]
        private sealed class JsonArrayWrapper
        {
            public VisualEffectEntryDto[] items;
        }

        public static readonly (string Folder, string Label)[] CategoryLabels =
        {
            ("Explosions", "爆炸"),
            ("Impacts", "冲击"),
            ("Lightning", "闪电"),
            ("Magic Bursts", "魔法爆发"),
            ("Fantasy Spells", "奇幻法术"),
            ("Sci-fi", "科幻"),
            ("Smoke Bursts", "烟雾"),
            ("Splatters", "溅射"),
            ("Symbols", "符号"),
        };

        public static string GetCategoryLabel(string categoryFolder)
        {
            if (string.IsNullOrEmpty(categoryFolder))
            {
                return categoryFolder ?? string.Empty;
            }

            for (var i = 0; i < CategoryLabels.Length; i++)
            {
                if (string.Equals(CategoryLabels[i].Folder, categoryFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return CategoryLabels[i].Label;
                }
            }

            return categoryFolder;
        }

        public static string GetAuthoringAbsolutePath()
        {
            return Path.Combine(
                Application.dataPath,
                ContentCatalogTableLoader.AuthoringRelativeFolder.Replace('/', Path.DirectorySeparatorChar),
                VisualEffectCatalog.FileName);
        }

        public static string GetStreamingAbsolutePath()
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                "ContentVisual",
                "tables",
                VisualEffectCatalog.FileName);
        }

        public static List<EditorRow> LoadAll(out string error)
        {
            error = null;
            var path = GetAuthoringAbsolutePath();
            if (!File.Exists(path))
            {
                path = GetStreamingAbsolutePath();
            }

            if (!File.Exists(path))
            {
                return new List<EditorRow>();
            }

            try
            {
                var raw = File.ReadAllText(path);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<JsonArrayWrapper>(wrapped);
                var result = new List<EditorRow>();
                if (list?.items == null)
                {
                    return result;
                }

                for (var i = 0; i < list.items.Length; i++)
                {
                    var dto = list.items[i];
                    if (dto == null || string.IsNullOrWhiteSpace(dto.id))
                    {
                        continue;
                    }

                    VisualEffectCatalog.Normalize(dto);
                    var row = new EditorRow { Dto = dto };
                    row.MarkSaved();
                    result.Add(row);
                }

                result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
                return result;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return new List<EditorRow>();
            }
        }

        public static bool TrySaveAll(IReadOnlyList<EditorRow> rows, out string error)
        {
            error = null;
            if (rows == null)
            {
                error = "rows is null";
                return false;
            }

            try
            {
                var dtos = new List<VisualEffectEntryDto>(rows.Count);
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row?.Dto == null || string.IsNullOrWhiteSpace(row.Dto.id))
                    {
                        continue;
                    }

                    VisualEffectCatalog.Normalize(row.Dto);
                    dtos.Add(row.Dto);
                }

                dtos.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                WriteDtoList(dtos);

                for (var i = 0; i < rows.Count; i++)
                {
                    rows[i]?.MarkSaved();
                }

                VisualEffectCatalog.Invalidate();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 扫描 Resources 特效树生成/刷新条目；保留已有条目的 fps/scale/displayName/timingBindings。
        /// </summary>
        public static List<EditorRow> ScanAndMerge(IReadOnlyList<EditorRow> existing, out string report)
        {
            var byId = new Dictionary<string, EditorRow>(StringComparer.Ordinal);
            if (existing != null)
            {
                for (var i = 0; i < existing.Count; i++)
                {
                    var row = existing[i];
                    if (row?.Dto == null || string.IsNullOrWhiteSpace(row.Dto.id))
                    {
                        continue;
                    }

                    byId[row.Dto.id] = row;
                }
            }

            var scanned = ScanDiskEntries();
            var added = 0;
            var kept = 0;
            var result = new List<EditorRow>(scanned.Count);

            for (var i = 0; i < scanned.Count; i++)
            {
                var fresh = scanned[i];
                if (byId.TryGetValue(fresh.id, out var old) && old.Dto != null)
                {
                    fresh.defaultFps = old.Dto.defaultFps;
                    fresh.defaultScale = old.Dto.defaultScale;
                    if (!string.IsNullOrWhiteSpace(old.Dto.displayName))
                    {
                        fresh.displayName = old.Dto.displayName;
                    }

                    if (old.Dto.timingBindings != null && old.Dto.timingBindings.Length > 0)
                    {
                        fresh.timingBindings = (string[])old.Dto.timingBindings.Clone();
                    }

                    kept++;
                }
                else
                {
                    added++;
                }

                var row = new EditorRow { Dto = fresh };
                // 扫描合并后视为已与磁盘一致，直到用户改动。
                row.MarkSaved();
                result.Add(row);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            report = "scanned=" + scanned.Count + " added=" + added + " sticky=" + kept;
            return result;
        }

        public static List<VisualEffectEntryDto> ScanDiskEntries()
        {
            var result = new List<VisualEffectEntryDto>();
            var root = VisualEffectContentArt.RootAssetFolder;
            if (!AssetDatabase.IsValidFolder(root))
            {
                // 迁移前仍可能在 Legacy 路径。
                root = VisualEffectContentArt.LegacyArtsEffectsFolder;
                if (!AssetDatabase.IsValidFolder(root))
                {
                    return result;
                }
            }

            var categoryGuids = AssetDatabase.FindAssets(string.Empty, new[] { root });
            var categoryFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < categoryGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(categoryGuids[i]);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                var parent = ParentFolder(path);
                if (string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
                {
                    categoryFolders.Add(path.Replace('\\', '/'));
                }
            }

            foreach (var categoryPath in categoryFolders)
            {
                var category = Path.GetFileName(categoryPath);
                var variantFolders = ListChildFolders(categoryPath);
                for (var v = 0; v < variantFolders.Count; v++)
                {
                    var variantPath = variantFolders[v];
                    var variantId = Path.GetFileName(variantPath);
                    var leafFolders = ListChildFolders(variantPath);
                    for (var l = 0; l < leafFolders.Count; l++)
                    {
                        var leafPath = leafFolders[l];
                        var leafName = Path.GetFileName(leafPath);
                        if (!TryParseLeaf(variantId, leafName, out var size, out var color))
                        {
                            continue;
                        }

                        var sheetPath = leafPath + "/spritesheet.png";
                        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(sheetPath)))
                        {
                            continue;
                        }

                        var id = BuildId(category, variantId, size, color);
                        result.Add(new VisualEffectEntryDto
                        {
                            id = id,
                            category = category,
                            variantId = variantId,
                            size = size,
                            color = color,
                            sheetPath = sheetPath.Replace('\\', '/'),
                            defaultFps = 12f,
                            defaultScale = 1f,
                            displayName = variantId,
                            timingBindings = Array.Empty<string>(),
                        });
                    }
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        public static string BuildId(string category, string variantId, string size, string color)
        {
            var cat = (category ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
            return "vfx." + cat + "." + (variantId ?? string.Empty).Trim()
                   + "." + (size ?? string.Empty).Trim() + "_" + (color ?? string.Empty).Trim();
        }

        public static bool TryParseLeaf(string variantId, string leafName, out string size, out string color)
        {
            size = string.Empty;
            color = string.Empty;
            if (string.IsNullOrEmpty(leafName) || string.IsNullOrEmpty(variantId))
            {
                return false;
            }

            var prefix = variantId + "_";
            if (!leafName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rest = leafName.Substring(prefix.Length);
            var underscore = rest.IndexOf('_');
            if (underscore <= 0 || underscore >= rest.Length - 1)
            {
                return false;
            }

            size = rest.Substring(0, underscore);
            color = rest.Substring(underscore + 1);
            return !string.IsNullOrEmpty(size) && !string.IsNullOrEmpty(color);
        }

        private static void WriteDtoList(IReadOnlyList<VisualEffectEntryDto> dtos)
        {
            var sb = new StringBuilder(dtos.Count * 256);
            sb.AppendLine("[");
            for (var i = 0; i < dtos.Count; i++)
            {
                var dto = dtos[i];
                var json = JsonUtility.ToJson(dto, true);
                var indented = IndentBlock(json, "    ");
                sb.Append(indented);
                if (i < dtos.Count - 1)
                {
                    sb.Append(',');
                }

                sb.AppendLine();
            }

            sb.Append(']');
            var text = sb.ToString();
            var utf8 = new UTF8Encoding(false);
            var authoring = GetAuthoringAbsolutePath();
            var streaming = GetStreamingAbsolutePath();
            Directory.CreateDirectory(Path.GetDirectoryName(authoring) ?? authoring);
            Directory.CreateDirectory(Path.GetDirectoryName(streaming) ?? streaming);
            File.WriteAllText(authoring, text, utf8);
            File.WriteAllText(streaming, text, utf8);
            AssetDatabase.ImportAsset(
                "Assets/Arts/ContentVisual/tables/" + VisualEffectCatalog.FileName);
            AssetDatabase.ImportAsset(
                "Assets/StreamingAssets/ContentVisual/tables/" + VisualEffectCatalog.FileName);
        }

        private static List<string> ListChildFolders(string parent)
        {
            var result = new List<string>();
            if (!AssetDatabase.IsValidFolder(parent))
            {
                return result;
            }

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { parent });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                if (string.Equals(ParentFolder(path), parent.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(path);
                }
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static string ParentFolder(string path)
        {
            var normalized = path.Replace('\\', '/');
            var slash = normalized.LastIndexOf('/');
            return slash > 0 ? normalized.Substring(0, slash) : string.Empty;
        }

        private static string IndentBlock(string json, string indent)
        {
            if (string.IsNullOrEmpty(json))
            {
                return indent + "{}";
            }

            var lines = json.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(json.Length + lines.Length * indent.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(indent);
                sb.Append(lines[i]);
            }

            return sb.ToString();
        }
    }
}
#endif
