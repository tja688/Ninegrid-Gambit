#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;

namespace NineGrid.Presentation.Cheat
{
    /// <summary>
    /// 作弊面板「添加遗物」搜索索引：从 <see cref="GameContentCatalog.Relics"/> 提取非归档遗物，
    /// 按「遗物名 / DefId / 描述」建搜索文本。纯逻辑无 Unity 场景依赖。
    /// </summary>
    public static class CheatToolRelicSearchIndex
    {
        public sealed class RelicEntry
        {
            public string DefId;
            public string DisplayName;
            public string SearchText;
        }

        /// <summary>
        /// 构建候选池。过滤：排除非正式遗物（归档 / AI 拓展，ADR-0054）。
        /// <paramref name="descriptionProvider"/> 可选注入表现层描述（运行时走 DTO）。
        /// </summary>
        public static List<RelicEntry> Build(
            GameContentCatalog catalog,
            Func<string, string> descriptionProvider = null)
        {
            var result = new List<RelicEntry>();
            if (catalog == null || catalog.Relics == null)
            {
                return result;
            }

            foreach (var pair in catalog.Relics)
            {
                var relic = pair.Value;
                if (relic == null || FormalContentWiring.IsUnofficialRelic(relic))
                {
                    continue;
                }

                var displayName = ResolveDisplayName(relic);
                result.Add(new RelicEntry
                {
                    DefId = relic.DefId,
                    DisplayName = displayName,
                    SearchText = BuildSearchText(relic, displayName, descriptionProvider),
                });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return result;
        }

        /// <summary>匹配：查询为空返回全部；否则不区分大小写子串匹配搜索文本。</summary>
        public static List<RelicEntry> Match(IReadOnlyList<RelicEntry> entries, string query)
        {
            var result = new List<RelicEntry>(entries != null ? entries.Count : 0);
            if (entries == null)
            {
                return result;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                result.AddRange(entries);
                return result;
            }

            var needle = query.Trim().ToLowerInvariant();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.SearchText != null && entry.SearchText.IndexOf(needle, StringComparison.Ordinal) >= 0)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>选项行文案：优先显示名，缺失时回退 DefId。</summary>
        public static string BuildOptionLabel(RelicEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DefId
                : entry.DisplayName.Trim();
        }

        private static string BuildSearchText(
            RelicContentDefinition relic,
            string displayName,
            Func<string, string> descriptionProvider)
        {
            var builder = new StringBuilder(96);
            AppendSegment(builder, displayName);
            AppendSegment(builder, relic.DefId);
            AppendSegment(builder, relic.DesignText);

            if (descriptionProvider != null)
            {
                AppendSegment(builder, descriptionProvider(relic.DefId));
            }

            return builder.ToString().ToLowerInvariant();
        }

        private static string ResolveDisplayName(RelicContentDefinition relic)
        {
            if (relic == null)
            {
                return string.Empty;
            }

            if (CardPresentationAuthority.TryGetOwnedDisplayName(relic.DefId, out var owned)
                && !string.IsNullOrWhiteSpace(owned))
            {
                return owned.Trim();
            }

            return relic.DisplayName ?? string.Empty;
        }

        private static void AppendSegment(StringBuilder builder, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(text);
        }
    }
}

#endif
