using System;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Flow
{
    /// <summary>
    /// 从内容 Catalog 解析帮助卡「使用时需选 N 张盘面卡」的需求（SelectedCards + zone=Board）。
    /// </summary>
    public static class HelpCardBoardSelectResolver
    {
        public static bool TryGetRequiredBoardSelectCount(string defId, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            var arch = NineGridArchitecture.Current;
            if (arch != null)
            {
                var catalog = arch.GetSystem<IContentSystem>()?.Catalog;
                if (catalog != null
                    && catalog.Cards.TryGetValue(defId, out var cardDef)
                    && TryParseFromCardEffects(catalog, cardDef, out count))
                {
                    return true;
                }
            }

            return TryHardcoded(defId, out count);
        }

        private static bool TryParseFromCardEffects(
            GameContentCatalog catalog,
            CardContentDefinition cardDef,
            out int count)
        {
            count = 0;
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

                if (TryParseSelectedCardsBoardCount(effect.Json, out count))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseSelectedCardsBoardCount(string json, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            if (json.IndexOf("\"SelectedCards\"", StringComparison.OrdinalIgnoreCase) < 0
                || json.IndexOf("\"Board\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            const string countKey = "\"count\":";
            var idx = json.IndexOf(countKey, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            idx += countKey.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx]))
            {
                idx++;
            }

            var end = idx;
            while (end < json.Length && char.IsDigit(json[end]))
            {
                end++;
            }

            if (end <= idx)
            {
                return false;
            }

            if (!int.TryParse(json.Substring(idx, end - idx), out count) || count <= 0)
            {
                count = 0;
                return false;
            }

            return true;
        }

        private static bool TryHardcoded(string defId, out int count)
        {
            count = 0;
            if (defId.IndexOf("swap_card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count = 2;
                return true;
            }

            if (defId.IndexOf("teleport_card", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count = 1;
                return true;
            }

            return false;
        }
    }
}
