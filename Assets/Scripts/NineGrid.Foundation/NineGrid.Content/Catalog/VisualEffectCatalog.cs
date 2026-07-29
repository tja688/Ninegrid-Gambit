using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 加载特效库表 visual_effects.json。权威目录同
    /// <see cref="ContentCatalogTableLoader"/>。
    /// </summary>
    public static class VisualEffectCatalog
    {
        public const string FileName = "visual_effects.json";

        private static Dictionary<string, VisualEffectEntryDto> sCache;
        private static List<VisualEffectEntryDto> sOrdered;

        public static void Invalidate()
        {
            sCache = null;
            sOrdered = null;
        }

        public static IReadOnlyList<VisualEffectEntryDto> All
        {
            get
            {
                EnsureLoaded();
                return sOrdered;
            }
        }

        public static bool TryGet(string id, out VisualEffectEntryDto entry)
        {
            EnsureLoaded();
            return sCache.TryGetValue(id ?? string.Empty, out entry);
        }

        public static int Count
        {
            get
            {
                EnsureLoaded();
                return sCache.Count;
            }
        }

        private static void EnsureLoaded()
        {
            if (sCache != null)
            {
                return;
            }

            sCache = new Dictionary<string, VisualEffectEntryDto>(StringComparer.Ordinal);
            sOrdered = new List<VisualEffectEntryDto>();
            var folder = ContentCatalogTableLoader.ResolveTablesDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            var path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                var raw = File.ReadAllText(path);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<JsonArrayWrapper>(wrapped);
                if (list?.items == null)
                {
                    return;
                }

                for (var i = 0; i < list.items.Length; i++)
                {
                    var row = list.items[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.id))
                    {
                        continue;
                    }

                    Normalize(row);
                    sCache[row.id] = row;
                    sOrdered.Add(row);
                }

                sOrdered.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[VisualEffectCatalog] Failed reading " + path + ": " + ex.Message);
            }
        }

        public static void Normalize(VisualEffectEntryDto row)
        {
            if (row == null)
            {
                return;
            }

            row.id = (row.id ?? string.Empty).Trim();
            row.category = row.category ?? string.Empty;
            row.variantId = row.variantId ?? string.Empty;
            row.size = row.size ?? string.Empty;
            row.color = row.color ?? string.Empty;
            row.sheetPath = (row.sheetPath ?? string.Empty).Replace('\\', '/').Trim();
            if (row.defaultFps < 0.01f)
            {
                row.defaultFps = 12f;
            }

            if (row.defaultScale < 0.01f)
            {
                row.defaultScale = 1f;
            }

            if (string.IsNullOrWhiteSpace(row.displayName))
            {
                row.displayName = row.variantId;
            }

            if (row.timingBindings == null)
            {
                row.timingBindings = Array.Empty<string>();
            }
        }

        [Serializable]
        private sealed class JsonArrayWrapper
        {
            public VisualEffectEntryDto[] items;
        }
    }
}
