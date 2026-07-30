using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Flow
{
    /// <summary>
    /// DevTest 快速测试：主菜单 <c>\0</c>–<c>\9</c> 效果体验通道预设。
    /// 技能列表可空；可选钉死首关牌组。正式开局不走此表。
    /// </summary>
    public static class QuickTestDeckCatalog
    {
        public const int MaxPickerCode = 9;

        public sealed class ChannelPreset
        {
            public ChannelPreset(
                string displayName,
                IReadOnlyList<string> skillIds = null,
                string pinnedFirstBattleDeckId = null,
                QuickTestNodeOrderMode nodeOrder = QuickTestNodeOrderMode.Shuffled)
            {
                DisplayName = displayName ?? string.Empty;
                SkillIds = skillIds ?? Array.Empty<string>();
                PinnedFirstBattleDeckId = pinnedFirstBattleDeckId;
                NodeOrder = nodeOrder;
            }

            public string DisplayName { get; }
            public IReadOnlyList<string> SkillIds { get; }
            public string PinnedFirstBattleDeckId { get; }
            public QuickTestNodeOrderMode NodeOrder { get; }
        }

        /// <summary>
        /// 批次 A：两通道预算——按触发轴混挂；储备技 <c>purge_followers</c> 不占码。
        /// <c>\1</c> 移除向；<c>\2</c> 互动向；其余空置留给后续批次。
        /// </summary>
        private static readonly ChannelPreset[] sPresets =
        {
            new ChannelPreset("通道0", Array.Empty<string>()),
            new ChannelPreset(
                "移除向",
                new[] { "skill.sacrifice", "skill.absorb", "skill.offer_fire" }),
            new ChannelPreset(
                "互动向",
                new[] { "skill.call_melee6", "skill.link_prep" }),
            new ChannelPreset("通道3", Array.Empty<string>()),
            new ChannelPreset("通道4", Array.Empty<string>()),
            new ChannelPreset("通道5", Array.Empty<string>()),
            new ChannelPreset("通道6", Array.Empty<string>()),
            new ChannelPreset("通道7", Array.Empty<string>()),
            new ChannelPreset("通道8", Array.Empty<string>()),
            new ChannelPreset("通道9", Array.Empty<string>()),
        };

        public static bool TryResolvePickerCode(int code, out ChannelPreset preset)
        {
            preset = null;
            if (code < 0 || code > MaxPickerCode || code >= sPresets.Length)
            {
                return false;
            }

            preset = sPresets[code];
            return preset != null;
        }

        /// <summary>兼容旧调用：解析通道并返回可选钉死牌组与显示名。</summary>
        public static bool TryResolvePickerCode(
            int code,
            GameContentCatalog catalog,
            out string deckId,
            out string displayName)
        {
            deckId = null;
            displayName = null;
            if (!TryResolvePickerCode(code, out var preset))
            {
                return false;
            }

            deckId = preset.PinnedFirstBattleDeckId;
            displayName = ResolveDisplayName(catalog, preset);
            return true;
        }

        public static int GetDefaultNodeIndexForDeckKind(MonsterDeckKind kind)
        {
            switch (kind)
            {
                case MonsterDeckKind.WeakElite:
                    return 1;
                case MonsterDeckKind.StrongElite:
                    return 4;
                case MonsterDeckKind.Boss:
                    return 7;
                default:
                    return 1;
            }
        }

        public static int GetDefaultNodeIndexForDeckId(GameContentCatalog catalog, string deckId)
        {
            if (catalog != null
                && !string.IsNullOrEmpty(deckId)
                && catalog.MonsterDecks.TryGetValue(deckId, out var deck)
                && deck != null)
            {
                return GetDefaultNodeIndexForDeckKind(deck.Kind);
            }

            return 1;
        }

        public static string BuildPickerMenuText(GameContentCatalog catalog)
        {
            var builder = new StringBuilder(320);
            builder.AppendLine("[快速测试 · 效果通道]");
            builder.AppendLine("HP99 ATK5 · 怪技能仅本通道动态挂");
            for (var code = 0; code <= MaxPickerCode; code++)
            {
                if (!TryResolvePickerCode(code, out var preset))
                {
                    continue;
                }

                builder.Append(code).Append(' ');
                builder.Append(ResolveDisplayName(catalog, preset));
                if (preset.SkillIds != null && preset.SkillIds.Count > 0)
                {
                    builder.Append(" (");
                    for (var i = 0; i < preset.SkillIds.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(',');
                        }

                        builder.Append(ShortSkillId(preset.SkillIds[i]));
                    }

                    builder.Append(')');
                }

                builder.AppendLine();
            }

            builder.Append("长按 \\ 选码，释放确认");
            return builder.ToString().TrimEnd();
        }

        private static string ResolveDisplayName(GameContentCatalog catalog, ChannelPreset preset)
        {
            if (preset == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(preset.PinnedFirstBattleDeckId)
                && catalog != null
                && catalog.MonsterDecks.TryGetValue(preset.PinnedFirstBattleDeckId, out var deck)
                && deck != null
                && !string.IsNullOrWhiteSpace(deck.DisplayName))
            {
                return preset.DisplayName + "·" + deck.DisplayName;
            }

            return preset.DisplayName;
        }

        private static string ShortSkillId(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return "?";
            }

            const string prefix = "skill.";
            return skillId.StartsWith(prefix, StringComparison.Ordinal)
                ? skillId.Substring(prefix.Length)
                : skillId;
        }
    }
}
