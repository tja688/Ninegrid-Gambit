using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 右键详情卡专属首条正文（策划「效果」行）。键 = contentId。
    /// 权威目录同 <see cref="ContentCatalogTableLoader"/>。
    /// </summary>
    public static class InspectEffectCopyCatalog
    {
        public const string FileName = "inspect_effect_copy.json";

        private static Dictionary<string, string> sCache;

        public static void Invalidate()
        {
            sCache = null;
        }

        public static bool TryGet(string contentId, out string effectText)
        {
            EnsureLoaded();
            effectText = string.Empty;
            if (string.IsNullOrWhiteSpace(contentId) || sCache == null)
            {
                return false;
            }

            if (!sCache.TryGetValue(contentId.Trim(), out var text)
                || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            effectText = text.Trim();
            return true;
        }

        private static void EnsureLoaded()
        {
            if (sCache != null)
            {
                return;
            }

            sCache = new Dictionary<string, string>(StringComparer.Ordinal);
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
                var dto = JsonUtility.FromJson<FileDto>(raw);
                if (dto == null || dto.entries == null)
                {
                    return;
                }

                for (var i = 0; i < dto.entries.Length; i++)
                {
                    var row = dto.entries[i];
                    if (row == null
                        || string.IsNullOrWhiteSpace(row.contentId)
                        || string.IsNullOrWhiteSpace(row.effectText))
                    {
                        continue;
                    }

                    sCache[row.contentId.Trim()] = row.effectText.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InspectEffectCopyCatalog] Failed reading " + path + ": " + ex.Message);
            }
        }

        [Serializable]
        private sealed class FileDto
        {
            public int schema;
            public EntryDto[] entries;
        }

        [Serializable]
        private sealed class EntryDto
        {
            public string contentId;
            public string effectText;
        }
    }
}
