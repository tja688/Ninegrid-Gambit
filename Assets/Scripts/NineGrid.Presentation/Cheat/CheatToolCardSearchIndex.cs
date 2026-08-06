#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Presentation.Cheat
{
    /// <summary>
    /// 作弊面板「战斗加卡」搜索索引：从 <see cref="GameContentCatalog"/> 提取可合法入卡组的卡
    /// （怪物 / 机关 / 道具三类，排除归档卡组），并按「卡名 / 卡组名 / 技能名 / 技能效果描述」建搜索文本。
    /// 纯逻辑无 Unity 场景依赖，EditMode 可测。
    /// </summary>
    public static class CheatToolCardSearchIndex
    {
        public sealed class CardEntry
        {
            public string DefId;
            public string DisplayName;
            public string DeckDisplayName;
            public CardKind Kind;
            public string SearchText;
        }

        /// <summary>
        /// 构建候选池。过滤规则：
        /// 1) 仅 Monster / Trap / HelpCard（战斗中卡组可合法出现的三类）；
        /// 2) 排除归档卡组成员（deck.help_archive / deck.relic_archive —— 表现层配置器冗余归档卡）。
        /// <paramref name="descriptionProvider"/> 可选注入卡面描述（运行时走表现层 DTO，测试可不传）。
        /// </summary>
        public static List<CardEntry> Build(
            GameContentCatalog catalog,
            Func<string, string> descriptionProvider = null)
        {
            var result = new List<CardEntry>();
            if (catalog == null || catalog.Cards == null)
            {
                return result;
            }

            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null)
                {
                    continue;
                }

                if (card.Kind != CardKind.Monster
                    && card.Kind != CardKind.Trap
                    && card.Kind != CardKind.HelpCard)
                {
                    continue;
                }

                if (card.Kind == CardKind.HelpCard && HelpCardDecks.IsArchive(card.DeckId))
                {
                    continue;
                }

                result.Add(new CardEntry
                {
                    DefId = card.DefId,
                    DisplayName = card.DisplayName,
                    DeckDisplayName = ResolveDeckDisplayName(catalog, card.DeckId),
                    Kind = card.Kind,
                    SearchText = BuildSearchText(catalog, card, descriptionProvider),
                });
            }

            result.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return result;
        }

        /// <summary>匹配：查询为空返回全部；否则不区分大小写子串匹配搜索文本。</summary>
        public static List<CardEntry> Match(IReadOnlyList<CardEntry> entries, string query)
        {
            var result = new List<CardEntry>(entries != null ? entries.Count : 0);
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

        /// <summary>选项行文案：卡组名 · 卡名（卡组名缺失时只显示卡名）。</summary>
        public static string BuildOptionLabel(CardEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            var name = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.DefId
                : entry.DisplayName.Trim();
            if (string.IsNullOrWhiteSpace(entry.DeckDisplayName))
            {
                return name;
            }

            return entry.DeckDisplayName.Trim() + " · " + name;
        }

        private static string BuildSearchText(
            GameContentCatalog catalog,
            CardContentDefinition card,
            Func<string, string> descriptionProvider)
        {
            var builder = new StringBuilder(96);
            AppendSegment(builder, card.DisplayName);
            AppendSegment(builder, card.DefId);
            AppendSegment(builder, ResolveDeckDisplayName(catalog, card.DeckId));

            if (card.SkillIds != null)
            {
                for (var i = 0; i < card.SkillIds.Count; i++)
                {
                    AppendSkillSearchText(builder, catalog, card.SkillIds[i]);
                }
            }

            if (descriptionProvider != null)
            {
                var description = descriptionProvider(card.DefId);
                AppendSegment(builder, description);
            }

            return builder.ToString().ToLowerInvariant();
        }

        private static void AppendSkillSearchText(
            StringBuilder builder,
            GameContentCatalog catalog,
            string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId) || catalog == null)
            {
                return;
            }

            if (catalog.TryGetSkill(skillId, out var skill) && skill != null)
            {
                AppendSegment(builder, skill.DisplayName);
                AppendSegment(builder, skill.DesignText);
                return;
            }

            AppendSegment(builder, skillId);
        }

        private static string ResolveDeckDisplayName(GameContentCatalog catalog, string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId))
            {
                return string.Empty;
            }

            if (catalog != null
                && catalog.MonsterDecks != null
                && catalog.MonsterDecks.TryGetValue(deckId, out var deck)
                && deck != null
                && !string.IsNullOrWhiteSpace(deck.DisplayName))
            {
                return deck.DisplayName.Trim();
            }

            return deckId.Trim();
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
