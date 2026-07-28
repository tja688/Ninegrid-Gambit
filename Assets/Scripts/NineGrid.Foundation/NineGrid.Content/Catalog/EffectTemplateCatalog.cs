using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 加载效果模板表（ADR-0009 / #70）。权威目录同
    /// <see cref="ContentCatalogTableLoader"/>。
    /// </summary>
    public static class EffectTemplateCatalog
    {
        public const string FileName = "effect_templates.json";

        private static Dictionary<string, EffectTemplateDefinition> sCache;

        public static void Invalidate()
        {
            sCache = null;
        }

        public static IReadOnlyDictionary<string, EffectTemplateDefinition> All
        {
            get
            {
                EnsureLoaded();
                return sCache;
            }
        }

        public static bool TryGet(string templateId, out EffectTemplateDefinition template)
        {
            EnsureLoaded();
            return sCache.TryGetValue(templateId ?? string.Empty, out template);
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

            sCache = new Dictionary<string, EffectTemplateDefinition>(StringComparer.Ordinal);
            var folder = ContentCatalogTableLoader.ResolveTablesDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogWarning("[EffectTemplateCatalog] tables directory not found.");
                return;
            }

            var path = Path.Combine(folder, FileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("[EffectTemplateCatalog] Missing " + path);
                return;
            }

            try
            {
                var raw = File.ReadAllText(path);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<JsonArrayWrapper<EffectTemplateRowDto>>(wrapped);
                if (list == null || list.items == null)
                {
                    return;
                }

                for (var i = 0; i < list.items.Length; i++)
                {
                    var row = list.items[i];
                    if (row == null || string.IsNullOrEmpty(row.id))
                    {
                        continue;
                    }

                    sCache[row.id] = new EffectTemplateDefinition(
                        row.id,
                        ParseStringArray(row.requires_json),
                        ParseStringArray(row.conditions_json),
                        row.body,
                        ParseEnum(row.state, ContentImplementationState.RawDesignOnly),
                        row.design_text);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[EffectTemplateCatalog] Failed reading " + path + ": " + ex.Message);
            }
        }

        private static string[] ParseStringArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<string>();
            }

            try
            {
                var node = EffectJson.Parse(json);
                var arr = node.AsArray();
                if (arr.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var result = new string[arr.Count];
                for (var i = 0; i < arr.Count; i++)
                {
                    result[i] = arr[i].AsString(string.Empty);
                }

                return result;
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            try
            {
                return (T)Enum.Parse(typeof(T), value, true);
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }

        [Serializable]
        private sealed class JsonArrayWrapper<T>
        {
            public T[] items;
        }

        [Serializable]
        private sealed class EffectTemplateRowDto
        {
            public string id;
            public string state;
            public string design_text;
            public string requires_json;
            public string conditions_json;
            public string body;
        }
    }
}
