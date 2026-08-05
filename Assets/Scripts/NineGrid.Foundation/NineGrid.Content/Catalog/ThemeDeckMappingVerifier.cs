using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 槽位映射正确性校验（#127 / ADR-0029）：把 <see cref="GameContentCatalog"/> 中七套正式卡组的
    /// 成员与 <see cref="ThemeDeckStableMapping"/> 逐槽比对，并校验结构不变量
    /// （重复 sequence、缺槽、越界成员、错误 Boss）。
    /// 只回答「映射正确」，不回答「正式可达」——后者归 <see cref="ThemeDeckFormalReadiness"/>。
    /// </summary>
    public static class ThemeDeckMappingVerifier
    {
        public sealed class Issue
        {
            public string DeckId;
            public string Code;
            public string Message;
        }

        /// <summary>七套全部：槽位比对 + 跨套复用检查。</summary>
        public static List<Issue> VerifySlotMapping(GameContentCatalog catalog)
        {
            var issues = new List<Issue>();
            if (catalog == null)
            {
                issues.Add(new Issue { Code = "catalog_null", Message = "目录为空。" });
                return issues;
            }

            for (var i = 0; i < ThemeDeckStableMapping.Entries.Count; i++)
            {
                VerifyDeckSlots(catalog, ThemeDeckStableMapping.Entries[i], issues);
            }

            VerifyNoCrossDeckReuse(issues);
            return issues;
        }

        /// <summary>七套全部：序列 5 恒为层主、序列 1–4 非层主。</summary>
        public static List<Issue> VerifyBossFlags(GameContentCatalog catalog)
        {
            var issues = new List<Issue>();
            if (catalog == null)
            {
                issues.Add(new Issue { Code = "catalog_null", Message = "目录为空。" });
                return issues;
            }

            for (var i = 0; i < ThemeDeckStableMapping.Entries.Count; i++)
            {
                VerifyDeckBossFlags(catalog, ThemeDeckStableMapping.Entries[i], issues);
            }

            return issues;
        }

        /// <summary>单套校验（槽位 + Boss 两层的并集）：供逐套迁移票与夹具测试使用。</summary>
        public static List<Issue> VerifyDeck(GameContentCatalog catalog, string deckId)
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

            VerifyDeckSlots(catalog, entry, issues);
            VerifyDeckBossFlags(catalog, entry, issues);
            return issues;
        }

        private static void VerifyDeckSlots(
            GameContentCatalog catalog,
            ThemeDeckStableEntry entry,
            List<Issue> issues)
        {
            var bySequence = new Dictionary<int, string>();
            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                if (!string.Equals(card.DeckId, entry.DeckId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (card.Sequence < 1 || card.Sequence > ThemeDeckStableMapping.SequenceCount)
                {
                    issues.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "member_out_of_range",
                        Message = entry.DeckId + " 的 " + card.DefId + " sequence=" + card.Sequence
                            + " 超出 1–5。",
                    });
                    continue;
                }

                if (bySequence.TryGetValue(card.Sequence, out var existing))
                {
                    issues.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "slot_duplicate",
                        Message = entry.DeckId + " sequence=" + card.Sequence
                            + " 同时挂 " + existing + " 与 " + card.DefId + "。",
                    });
                    continue;
                }

                bySequence[card.Sequence] = card.DefId;
            }

            for (var seq = 1; seq <= ThemeDeckStableMapping.SequenceCount; seq++)
            {
                var expected = entry.GetContentId(seq);
                if (!bySequence.TryGetValue(seq, out var actual))
                {
                    issues.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "slot_gap",
                        Message = entry.DeckId + " 缺少 sequence=" + seq + " 的怪物（契约期望 " + expected + "）。",
                    });
                    continue;
                }

                if (!string.Equals(actual, expected, System.StringComparison.Ordinal))
                {
                    issues.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "slot_mismatch",
                        Message = entry.DeckId + " sequence=" + seq + " 实际为 " + actual
                            + "，契约期望 " + expected + "。",
                    });
                }
            }
        }

        private static void VerifyDeckBossFlags(
            GameContentCatalog catalog,
            ThemeDeckStableEntry entry,
            List<Issue> issues)
        {
            foreach (var pair in CollectDeckMembers(catalog, entry))
            {
                var card = pair.Value;
                var shouldBeBoss = card.Sequence == 5;
                if (card.IsBoss != shouldBeBoss)
                {
                    issues.Add(new Issue
                    {
                        DeckId = entry.DeckId,
                        Code = "boss_flag",
                        Message = entry.DeckId + " " + card.DefId + " sequence=" + card.Sequence
                            + " IsBoss=" + card.IsBoss + "（期望 " + shouldBeBoss
                            + "：序列 5 恒为层主，1–4 非层主）。",
                    });
                }
            }
        }

        private static Dictionary<int, CardContentDefinition> CollectDeckMembers(
            GameContentCatalog catalog,
            ThemeDeckStableEntry entry)
        {
            var result = new Dictionary<int, CardContentDefinition>();
            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                if (!string.Equals(card.DeckId, entry.DeckId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result[card.Sequence] = card;
            }

            return result;
        }

        private static void VerifyNoCrossDeckReuse(List<Issue> issues)
        {
            var owner = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < ThemeDeckStableMapping.Entries.Count; i++)
            {
                var entry = ThemeDeckStableMapping.Entries[i];
                var slots = entry.SequenceContentIds;
                for (var s = 0; s < slots.Count; s++)
                {
                    var contentId = slots[s];
                    if (owner.TryGetValue(contentId, out var prior))
                    {
                        issues.Add(new Issue
                        {
                            DeckId = entry.DeckId,
                            Code = "content_reused",
                            Message = contentId + " 同时是 " + prior + " 与 " + entry.DeckId
                                + " 的稳定槽位。",
                        });
                    }
                    else
                    {
                        owner[contentId] = entry.DeckId;
                    }
                }
            }
        }
    }
}
