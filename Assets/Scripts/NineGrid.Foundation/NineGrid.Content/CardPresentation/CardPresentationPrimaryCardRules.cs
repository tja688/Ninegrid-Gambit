using System;
using System.Collections.Generic;
using NineGrid.Core.Content;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 表现层「主要内容卡牌」与归档排除规则（CONTEXT / ADR-0035）。
    /// 批量描述导出/导入与编辑器侧栏过滤共用。
    /// </summary>
    public static class CardPresentationPrimaryCardRules
    {
        public const string TransitionDeckId = "deck.transition";

        /// <summary>怪物 / 遗物 / 道具 / 机关四类玩法卡。</summary>
        public static bool IsPrimaryKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            return string.Equals(kind, "Monster", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "Relic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "HelpCard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "Trap", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 卡组在表现层编辑器中的显示名是否带「归档」标记（非正式接线）。
        /// </summary>
        public static bool DeckDisplayNameMarksArchive(string deckDisplayName)
        {
            if (string.IsNullOrWhiteSpace(deckDisplayName))
            {
                return false;
            }

            return deckDisplayName.Trim().Contains("归档", StringComparison.Ordinal);
        }

        /// <summary>
        /// 是否应排除在正式接线 / 批量描述导出之外。
        /// </summary>
        public static bool IsArchivedMember(
            CardPresentationConfigDto dto,
            IReadOnlyDictionary<string, string> deckDisplayNamesByDeckId)
        {
            if (dto == null)
            {
                return true;
            }

            var deckId = dto.deckId?.Trim() ?? string.Empty;
            if (HelpCardDecks.IsArchive(deckId) || RelicDecks.IsArchive(deckId))
            {
                return true;
            }

            if (string.Equals(deckId, TransitionDeckId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(dto.kind, "Monster", StringComparison.OrdinalIgnoreCase) && dto.isReserve)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(deckId)
                && deckDisplayNamesByDeckId != null
                && deckDisplayNamesByDeckId.TryGetValue(deckId, out var deckDisplayName)
                && DeckDisplayNameMarksArchive(deckDisplayName))
            {
                return true;
            }

            return false;
        }

        /// <summary>正式接线的主要内容卡牌。</summary>
        public static bool IsWiredPrimaryCard(
            CardPresentationConfigDto dto,
            IReadOnlyDictionary<string, string> deckDisplayNamesByDeckId)
        {
            return dto != null
                && IsPrimaryKind(dto.kind)
                && !IsArchivedMember(dto, deckDisplayNamesByDeckId);
        }
    }
}
