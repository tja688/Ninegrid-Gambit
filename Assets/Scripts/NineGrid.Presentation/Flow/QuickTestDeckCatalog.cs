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
        /// 通道预算——同通道多技能按格号升序一怪一技分发（见 <c>BattleSessionCheat</c>）；
        /// 储备技 <c>purge_followers</c> 不占码。
        /// <c>\1</c> 移除向；<c>\2</c> 互动向；<c>\3</c> 翻面向；
        /// <c>\4</c> 批次1交战翻面；<c>\5</c> 批次1移动互动。
        /// 列表顺序 = 挂载顺序（格号小→大）。
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
            new ChannelPreset(
                "翻面向",
                new[] { "skill.leap_kill", "skill.steal" }),
            new ChannelPreset(
                "批次1交战翻面",
                new[]
                {
                    "skill.recuperate",
                    "skill.rise_up",
                    "skill.evade",
                    "skill.battle_hardened",
                }),
            new ChannelPreset(
                "批次1移动互动",
                new[]
                {
                    "skill.link_tactics",
                    "skill.delivery",
                    "skill.flame_boiling",
                }),
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

        /// <summary>
        /// QuickTest 卡面临时短描述上限（严格小于此值）。非正式文案权威，后续整管线废弃。
        /// </summary>
        public const int CardFaceDescriptionMaxLength = 16;

        /// <summary>
        /// 单技能卡面短描述（手动预设，非 DesignText）。多技能时优先拼短描述，塞不下再拼显示名。
        /// </summary>
        private static readonly Dictionary<string, string> sCardFaceShortBriefs =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "skill.sacrifice", "移除时友攻+1" },
                { "skill.absorb", "邻死随机强化" },
                { "skill.offer_fire", "死时加烈焰" },
                { "skill.call_melee6", "互动5召近战6" },
                { "skill.link_prep", "互动5去邻道具" },
                { "skill.leap_kill", "翻面邻攻伤人" },
                { "skill.steal", "翻面盗邻帮助" },
                { "skill.recuperate", "翻面攻+1甲+2" },
                { "skill.rise_up", "伤人翻其他怪" },
                { "skill.delivery", "移5换邻道具" },
                { "skill.link_tactics", "邻怪攻+1光环" },
                { "skill.evade", "战后换四角" },
                { "skill.battle_hardened", "伤2自攻+1" },
                { "skill.flame_boiling", "互动5烈焰+1" },
            };

        /// <summary>多技能卡面用的短显示名（catalog 未就绪时兜底）。</summary>
        private static readonly Dictionary<string, string> sCardFaceShortNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "skill.sacrifice", "献身" },
                { "skill.absorb", "吸收" },
                { "skill.offer_fire", "献火" },
                { "skill.call_melee6", "呼唤" },
                { "skill.link_prep", "链接" },
                { "skill.leap_kill", "跳杀" },
                { "skill.steal", "盗取" },
                { "skill.recuperate", "休养" },
                { "skill.rise_up", "起来" },
                { "skill.delivery", "快递" },
                { "skill.link_tactics", "链接战术" },
                { "skill.evade", "逃避" },
                { "skill.battle_hardened", "历战" },
                { "skill.flame_boiling", "烈焰沸腾" },
            };

        /// <summary>
        /// QuickTest 覆写卡面用：最终文案严格少于 <see cref="CardFaceDescriptionMaxLength"/> 字。
        /// </summary>
        public static string BuildCardFaceDescription(
            GameContentCatalog catalog,
            IReadOnlyList<string> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0)
            {
                return string.Empty;
            }

            string text;
            if (skillIds.Count == 1)
            {
                text = ResolveSingleSkillBrief(catalog, skillIds[0]);
            }
            else if (TryJoinShortBriefsUnderLimit(skillIds, out var joinedBriefs))
            {
                text = joinedBriefs;
            }
            else
            {
                text = BuildMultiSkillNameBrief(catalog, skillIds);
            }

            return ClampCardFaceDescription(text);
        }

        private static string ResolveSingleSkillBrief(GameContentCatalog catalog, string skillId)
        {
            if (!string.IsNullOrEmpty(skillId)
                && sCardFaceShortBriefs.TryGetValue(skillId, out var brief)
                && !string.IsNullOrWhiteSpace(brief))
            {
                return brief.Trim();
            }

            if (catalog != null
                && !string.IsNullOrEmpty(skillId)
                && catalog.TryGetSkill(skillId, out var skill)
                && skill != null
                && !string.IsNullOrWhiteSpace(skill.DisplayName))
            {
                return skill.DisplayName.Trim();
            }

            return ShortSkillId(skillId);
        }

        /// <summary>
        /// 多技能时若每条都有短描述且用 · 拼接后仍 &lt;16，则用短描述；否则交给显示名拼接。
        /// </summary>
        private static bool TryJoinShortBriefsUnderLimit(
            IReadOnlyList<string> skillIds,
            out string joined)
        {
            joined = null;
            var builder = new StringBuilder(24);
            for (var i = 0; i < skillIds.Count; i++)
            {
                var skillId = skillIds[i];
                if (string.IsNullOrEmpty(skillId)
                    || !sCardFaceShortBriefs.TryGetValue(skillId, out var brief)
                    || string.IsNullOrWhiteSpace(brief))
                {
                    return false;
                }

                if (builder.Length > 0)
                {
                    builder.Append('·');
                }

                builder.Append(brief.Trim());
                if (builder.Length >= CardFaceDescriptionMaxLength)
                {
                    return false;
                }
            }

            if (builder.Length == 0)
            {
                return false;
            }

            joined = builder.ToString();
            return true;
        }

        private static string BuildMultiSkillNameBrief(
            GameContentCatalog catalog,
            IReadOnlyList<string> skillIds)
        {
            var builder = new StringBuilder(24);
            for (var i = 0; i < skillIds.Count; i++)
            {
                var name = ResolveSkillDisplayName(catalog, skillIds[i]);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('·');
                }

                builder.Append(name);
            }

            return builder.ToString();
        }

        private static string ResolveSkillDisplayName(GameContentCatalog catalog, string skillId)
        {
            if (!string.IsNullOrEmpty(skillId)
                && sCardFaceShortNames.TryGetValue(skillId, out var shortName)
                && !string.IsNullOrWhiteSpace(shortName))
            {
                return shortName.Trim();
            }

            if (catalog != null
                && !string.IsNullOrEmpty(skillId)
                && catalog.TryGetSkill(skillId, out var skill)
                && skill != null
                && !string.IsNullOrWhiteSpace(skill.DisplayName))
            {
                return skill.DisplayName.Trim();
            }

            return ShortSkillId(skillId);
        }

        private static string ClampCardFaceDescription(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            text = text.Trim();
            if (text.Length < CardFaceDescriptionMaxLength)
            {
                return text;
            }

            return text.Substring(0, CardFaceDescriptionMaxLength - 1);
        }
    }
}
