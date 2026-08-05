using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 正式可达性前置报告（#127 / ADR-0029）：当前数据是否可直接正式启用七套。
    /// 与 <see cref="ThemeDeckMappingVerifier"/> 分层——映射正确 ≠ 正式可达。
    /// 后续各套迁移票以 <see cref="Report.Blockers"/> 为机器可验证清单，逐项清空后 <see cref="Report.Reachable"/>。
    /// </summary>
    public static class ThemeDeckFormalReadiness
    {
        /// <summary>过渡卡组（ADR-0022 / #86）：正式启用前须清空可用怪物。</summary>
        public const string TransitionDeckId = "deck.transition";

        public sealed class Report
        {
            public bool Reachable;
            public List<Issue> Blockers = new List<Issue>();
        }

        public sealed class Issue
        {
            public string Code;
            public string DeckId;
            public string Message;
        }

        public static Report Evaluate(GameContentCatalog catalog)
        {
            var report = new Report();
            if (catalog == null)
            {
                report.Blockers.Add(new Issue { Code = "catalog_null", Message = "目录为空。" });
                return report;
            }

            for (var i = 0; i < ThemeDeckStableMapping.Entries.Count; i++)
            {
                var entry = ThemeDeckStableMapping.Entries[i];
                if (!catalog.MonsterDecks.TryGetValue(entry.DeckId, out var deck) || deck == null)
                {
                    report.Blockers.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "deck_not_in_table",
                        Message = entry.DeckId + " 未登记在 monster_decks.json。",
                    });
                    continue;
                }

                if (deck.Kind == MonsterDeckKind.Reserve)
                {
                    report.Blockers.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "deck_reserve",
                        Message = entry.DeckId + " 在 monster_decks.json 中仍为 Reserve，无法被每层主题绑定。",
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
                        report.Blockers.Add(new Issue
                        {
                            DeckId = entry.DeckId,
                            Code = "slot_card_reserve",
                            Message = entry.DeckId + " sequence=" + seq + "（" + contentId
                                + "）仍为 Reserve，按序列抽卡会缺档。",
                        });
                    }
                }
            }

            var stillInTransition = 0;
            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null || card.Kind != CardKind.Monster || card.IsReserve)
                {
                    continue;
                }

                if (string.Equals(card.DeckId, TransitionDeckId, System.StringComparison.OrdinalIgnoreCase))
                {
                    stillInTransition++;
                }
            }

            if (stillInTransition > 0)
            {
                report.Blockers.Add(new Issue
                {
                    DeckId = TransitionDeckId,
                    Code = "transition_not_cleared",
                    Message = "过渡卡组仍有 " + stillInTransition + " 张可用怪物。",
                });
            }

            report.Reachable = report.Blockers.Count == 0;
            return report;
        }
    }
}
