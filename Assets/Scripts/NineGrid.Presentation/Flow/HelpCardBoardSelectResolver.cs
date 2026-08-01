using System;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Flow
{
    /// <summary>
    /// 帮助卡打出目标解析：无指向 / 单目标拖拽(count=1) / 盘面多选(count>=2)。
    /// </summary>
    public enum HelpCardPlayKind
    {
        None = 0,
        SingleDragTarget = 1,
        MultiBoardSelect = 2,
    }

    /// <summary>
    /// Catalog SelectedCards 原子解析结果（zone=Board）。
    /// </summary>
    public readonly struct HelpCardSelectedCardsSpec
    {
        public HelpCardSelectedCardsSpec(int count, string kindFilter, bool trueMonsterOnly = false)
        {
            Count = count;
            KindFilter = kindFilter;
            TrueMonsterOnly = trueMonsterOnly;
        }

        public int Count { get; }

        /// <summary>Monster 等；null/空表示任意非 Avatar 盘面卡。</summary>
        public string KindFilter { get; }

        /// <summary>ADR-0017：SelectedCards trueMonsterOnly（绑架等仅真怪）。</summary>
        public bool TrueMonsterOnly { get; }

        /// <summary>有 kind=Monster 过滤（可交战或真怪）。</summary>
        public bool RequiresMonster =>
            !string.IsNullOrEmpty(KindFilter)
            && string.Equals(KindFilter, "Monster", StringComparison.OrdinalIgnoreCase);

        /// <summary>可交战桶 Monster|Trap（飞刀等指向伤）。</summary>
        public bool RequiresCombatTarget => RequiresMonster && !TrueMonsterOnly;

        /// <summary>仅真怪 Monster。</summary>
        public bool RequiresTrueMonster => RequiresMonster && TrueMonsterOnly;
    }

    /// <summary>
    /// 从内容 Catalog 解析帮助卡打出时的目标需求。
    /// </summary>
    public static class HelpCardBoardSelectResolver
    {
        public static bool TryGetPlayKind(string defId, out HelpCardPlayKind kind, out HelpCardSelectedCardsSpec spec)
        {
            kind = HelpCardPlayKind.None;
            spec = default;
            if (!TryResolveSelectedCardsSpec(defId, out spec))
            {
                return true;
            }

            if (spec.Count >= 2)
            {
                kind = HelpCardPlayKind.MultiBoardSelect;
            }
            else if (spec.Count == 1)
            {
                kind = HelpCardPlayKind.SingleDragTarget;
            }

            return true;
        }

        /// <summary>仅 count>=2 的多选模式。</summary>
        public static bool TryGetRequiredBoardSelectCount(string defId, out int count)
        {
            count = 0;
            if (!TryResolveSelectedCardsSpec(defId, out var spec) || spec.Count < 2)
            {
                return false;
            }

            count = spec.Count;
            return true;
        }

        /// <summary>单目标拖拽（count=1）。</summary>
        public static bool TryGetSingleTargetSpec(string defId, out HelpCardSelectedCardsSpec spec)
        {
            spec = default;
            if (!TryResolveSelectedCardsSpec(defId, out spec) || spec.Count != 1)
            {
                return false;
            }

            return true;
        }

        /// <summary>多选模式默认描述（无 hover 时回退展示）。</summary>
        public static bool TryGetBoardSelectPrompt(string defId, out string prompt)
        {
            prompt = null;
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            if (TryResolveUseEffectDescription(defId, out var fromEffect))
            {
                prompt = BuildBoardSelectPrompt(fromEffect);
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    return true;
                }
            }

            if (defId.IndexOf("swap_card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                prompt = "选择两张卡牌互换位置";
                return true;
            }

            if (TryResolveSelectedCardsSpec(defId, out var spec) && spec.Count >= 2)
            {
                prompt = $"选择{spec.Count}张卡牌";
                return true;
            }

            return false;
        }

        private static bool TryResolveSelectedCardsSpec(string defId, out HelpCardSelectedCardsSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var content = arch.GetSystem<IContentSystem>();
                content?.TryReloadFromConfig();
                var catalog = content?.Catalog;
                if (catalog != null
                    && catalog.Cards.TryGetValue(defId, out var cardDef)
                    && TryParseFromCardEffects(catalog, cardDef, out spec))
                {
                    return true;
                }
            }

            return TryHardcodedSpec(defId, out spec);
        }

        private static bool TryParseFromCardEffects(
            GameContentCatalog catalog,
            CardContentDefinition cardDef,
            out HelpCardSelectedCardsSpec spec)
        {
            spec = default;
            if (cardDef?.EffectIds == null)
            {
                return false;
            }

            for (var i = 0; i < cardDef.EffectIds.Count; i++)
            {
                var effectId = cardDef.EffectIds[i];
                if (string.IsNullOrEmpty(effectId)
                    || !effectId.EndsWith(".use", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!catalog.Effects.TryGetValue(effectId, out var effect)
                    || effect == null
                    || string.IsNullOrEmpty(effect.Json))
                {
                    continue;
                }

                if (TryParseSelectedCardsBoardSpec(effect.Json, out spec))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseSelectedCardsBoardSpec(string json, out HelpCardSelectedCardsSpec spec)
        {
            spec = default;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            const string selectedCardsAtom = "\"atom\":\"SelectedCards\"";
            var atomIdx = json.IndexOf(selectedCardsAtom, StringComparison.OrdinalIgnoreCase);
            if (atomIdx < 0)
            {
                return false;
            }

            var sliceStart = Math.Max(0, atomIdx - 1);
            var sliceLength = Math.Min(360, json.Length - sliceStart);
            var slice = json.Substring(sliceStart, sliceLength);

            if (slice.IndexOf("\"Board\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (!TryParseJsonIntField(slice, "count", out var count) || count <= 0)
            {
                return false;
            }

            TryParseJsonStringField(slice, "kind", out var kindFilter);
            var trueMonsterOnly = TryParseJsonBoolField(slice, "trueMonsterOnly", out var flag) && flag;
            spec = new HelpCardSelectedCardsSpec(count, kindFilter, trueMonsterOnly);
            return true;
        }

        private static bool TryParseJsonBoolField(string json, string fieldName, out bool value)
        {
            value = false;
            var key = "\"" + fieldName + "\":";
            var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            idx += key.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
            {
                idx++;
            }

            if (idx + 4 <= json.Length
                && string.Compare(json, idx, "true", 0, 4, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = true;
                return true;
            }

            if (idx + 5 <= json.Length
                && string.Compare(json, idx, "false", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                value = false;
                return true;
            }

            return false;
        }

        private static bool TryParseJsonIntField(string json, string fieldName, out int value)
        {
            value = 0;
            var key = "\"" + fieldName + "\":";
            var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            idx += key.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
            {
                idx++;
            }

            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            {
                end++;
            }

            if (end <= idx)
            {
                return false;
            }

            return int.TryParse(json.Substring(idx, end - idx), out value);
        }

        private static bool TryParseJsonStringField(string json, string fieldName, out string value)
        {
            value = null;
            var key = "\"" + fieldName + "\":";
            var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            idx += key.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
            {
                idx++;
            }

            if (idx >= json.Length || json[idx] != '"')
            {
                return false;
            }

            idx++;
            var end = json.IndexOf('"', idx);
            if (end < 0)
            {
                return false;
            }

            value = json.Substring(idx, end - idx);
            return true;
        }

        private static bool TryHardcodedSpec(string defId, out HelpCardSelectedCardsSpec spec)
        {
            spec = default;
            if (defId.IndexOf("swap_card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                spec = new HelpCardSelectedCardsSpec(2, null);
                return true;
            }

            return false;
        }

        private static bool TryResolveUseEffectDescription(string defId, out string description)
        {
            description = null;
            var arch = NineGridArchitecture.Current;
            var catalog = arch?.GetSystem<IContentSystem>()?.Catalog;
            if (catalog == null
                || !catalog.Cards.TryGetValue(defId, out var cardDef)
                || cardDef?.EffectIds == null)
            {
                return false;
            }

            for (var i = 0; i < cardDef.EffectIds.Count; i++)
            {
                var effectId = cardDef.EffectIds[i];
                if (string.IsNullOrEmpty(effectId)
                    || !effectId.EndsWith(".use", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!catalog.Effects.TryGetValue(effectId, out var effect) || effect == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(effect.DesignText))
                {
                    description = effect.DesignText;
                    return true;
                }
            }

            return false;
        }

        private static string BuildBoardSelectPrompt(string effectDescription)
        {
            if (string.IsNullOrWhiteSpace(effectDescription))
            {
                return null;
            }

            var text = effectDescription.Trim();
            const string usePrefix = "[使用时]";
            var idx = text.IndexOf(usePrefix, StringComparison.Ordinal);
            if (idx >= 0)
            {
                text = text.Substring(idx + usePrefix.Length).Trim();
            }

            if (text.StartsWith("选择", StringComparison.Ordinal))
            {
                return text;
            }

            if (text.StartsWith("对", StringComparison.Ordinal))
            {
                return "请" + text;
            }

            return text;
        }
    }
}
