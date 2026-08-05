using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 窄内容契约（#128）：基础 / 链接两套的逐套可交付校验。
    /// 校验内容：槽位映射正确（含重复/缺槽/越界/错位）、Boss 标志、槽位卡非 Reserve、牌组已登记。
    /// 与 <see cref="ThemeDeckFormalReadiness"/>（全局正式可达性）分层：本契约不要求
    /// deck_kind 非 Reserve、不清空过渡组——#128 只交付两套元数据，不开放全局正式轮换。
    /// </summary>
    public static class ThemeDeckNarrowContract
    {
        /// <summary>本票窄契约覆盖的两套：基础（deck.dragon）、链接（deck.smallanimal）。</summary>
        public static readonly IReadOnlyList<string> NarrowScopeDeckIds = new[]
        {
            "deck.dragon",
            "deck.smallanimal",
        };

        public sealed class Issue
        {
            public string DeckId;
            public string Code;
            public string Message;
        }

        /// <summary>单套窄契约校验。</summary>
        public static List<Issue> Verify(GameContentCatalog catalog, string deckId)
        {
            var issues = new List<Issue>();
            if (catalog == null)
            {
                issues.Add(new Issue { DeckId = deckId, Code = "catalog_null", Message = "目录为空。" });
                return issues;
            }

            if (!ThemeDeckStableMapping.TryGet(deckId, out var entry))
            {
                issues.Add(new Issue
                {
                    DeckId = deckId,
                    Code = "deck_not_in_contract",
                    Message = deckId + " 不在七套稳定契约中。",
                });
                return issues;
            }

            var slotIssues = ThemeDeckMappingVerifier.VerifyDeck(catalog, deckId);
            for (var i = 0; i < slotIssues.Count; i++)
            {
                issues.Add(new Issue
                {
                    DeckId = slotIssues[i].DeckId,
                    Code = slotIssues[i].Code,
                    Message = slotIssues[i].Message,
                });
            }

            if (!catalog.MonsterDecks.TryGetValue(deckId, out var deck) || deck == null)
            {
                issues.Add(new Issue
                {
                    DeckId = deckId,
                    Code = "deck_not_in_table",
                    Message = deckId + " 未登记在 monster_decks.json。",
                });
            }

            for (var seq = 1; seq <= ThemeDeckStableMapping.SequenceCount; seq++)
            {
                var contentId = entry.GetContentId(seq);
                if (string.IsNullOrEmpty(contentId))
                {
                    continue;
                }

                if (catalog.TryGetCard(contentId, out var card) && card != null && card.IsReserve)
                {
                    issues.Add(new Issue
                    {
                        DeckId = deckId,
                        Code = "slot_card_reserve",
                        Message = deckId + " sequence=" + seq + "（" + contentId
                            + "）仍为 Reserve，按序列抽卡会缺档。",
                    });
                }
            }

            return issues;
        }

        /// <summary>窄契约覆盖套全部校验。</summary>
        public static List<Issue> VerifyAllInScope(GameContentCatalog catalog)
        {
            var issues = new List<Issue>();
            for (var i = 0; i < NarrowScopeDeckIds.Count; i++)
            {
                issues.AddRange(Verify(catalog, NarrowScopeDeckIds[i]));
            }

            return issues;
        }
    }
}
