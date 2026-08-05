using System.Collections.Generic;
using System.IO;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #128：基础 / 链接两套窄内容契约。
    /// 窄契约 = 槽位映射正确 + Boss 标志 + 槽位卡非 Reserve + 牌组已登记 + 双侧 JSON 一致；
    /// 本票不开放全局正式轮换（deck_kind 仍全表 Reserve、过渡组未清空）。
    /// 只断言稳定 ID / 槽位 / sequence / Reserve；禁止断言可变 displayName。
    /// </summary>
    public sealed class ThemeDeckNarrowContractTests
    {
        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            MonsterDeckTableCatalog.Invalidate();
        }

        [Test]
        public void NarrowScope_TwoDecks_PassNarrowContract()
        {
            var catalog = ContentCatalogBootstrap.Load();
            var issues = ThemeDeckNarrowContract.VerifyAllInScope(catalog);
            Assert.AreEqual(0, issues.Count, FormatIssues(issues));
        }

        [Test]
        public void NarrowScope_GlobalFormalRotation_OpenAfterArchival()
        {
            var catalog = ContentCatalogBootstrap.Load();
            var report = ThemeDeckFormalReadiness.Evaluate(catalog);
            Assert.IsTrue(
                report.Reachable,
                "#134 已归档过渡内容并启用七套：全局正式轮换应开放。\n" + FormatBlockers(report));
            Assert.IsFalse(HasCode(report.Blockers, "deck_reserve"), "七套已切 Unknown，不得再报 deck_reserve");
            Assert.IsFalse(HasCode(report.Blockers, "transition_not_cleared"), "过渡组已归档，不得再报 transition_not_cleared");
            Assert.IsFalse(HasCode(report.Blockers, "slot_card_reserve"), "七套槽位卡均已非 Reserve");
        }

        [Test]
        public void NarrowScope_CardJson_AuthoringMatchesStreaming()
        {
            for (var i = 0; i < ThemeDeckNarrowContract.NarrowScopeDeckIds.Count; i++)
            {
                var deckId = ThemeDeckNarrowContract.NarrowScopeDeckIds[i];
                Assert.IsTrue(ThemeDeckStableMapping.TryGet(deckId, out var entry), "契约缺失：" + deckId);
                for (var seq = 1; seq <= ThemeDeckStableMapping.SequenceCount; seq++)
                {
                    var contentId = entry.GetContentId(seq);
                    AssertAreEqualFiles(
                        CardPresentationJsonIO.GetAuthoringAbsolutePath(contentId),
                        CardPresentationJsonIO.GetStreamingAbsolutePath(contentId),
                        contentId + " 卡面 JSON 双侧不一致（Authoring 与 Streaming 必须同内容）");
                }
            }

            AssertAreEqualFiles(
                Path.GetFullPath(Path.Combine(Application.dataPath, "Arts", "ContentVisual", "tables", "monster_decks.json")),
                Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, "ContentVisual", "tables", "monster_decks.json")),
                "monster_decks.json 双侧不一致");
        }

        [Test]
        public void NarrowContract_ReportsReserveSlotCard()
        {
            var catalog = BuildContractCatalog(c =>
            {
                if (c.TryGetCard(Dragon.GetContentId(2), out var reservedSlot))
                {
                    reservedSlot.AsReserve();
                }
            });

            var issues = ThemeDeckNarrowContract.Verify(catalog, Dragon.DeckId);
            Assert.IsTrue(HasCode(issues, "slot_card_reserve"), "应报槽位卡 Reserve：\n" + FormatIssues(issues));
        }

        [Test]
        public void NarrowContract_ReportsMissingDeckTableEntry()
        {
            var catalog = new GameContentCatalog();
            for (var seq = 1; seq <= ThemeDeckStableMapping.SequenceCount; seq++)
            {
                catalog.AddCard(
                    new CardContentDefinition(Dragon.GetContentId(seq), "契约" + seq, CardKind.Monster)
                        .WithSequence(seq)
                        .WithAttackPattern(AttackPattern.OrthogonalMelee)
                        .InDeck(Dragon.DeckId));
            }

            var issues = ThemeDeckNarrowContract.Verify(catalog, Dragon.DeckId);
            Assert.IsTrue(HasCode(issues, "deck_not_in_table"), "应报牌组未登记：\n" + FormatIssues(issues));
        }

        private static readonly ThemeDeckStableEntry Dragon = ThemeDeckStableMapping.Entries[0];

        private static GameContentCatalog BuildContractCatalog(System.Action<GameContentCatalog> customize)
        {
            var catalog = new GameContentCatalog();
            for (var i = 0; i < ThemeDeckStableMapping.Entries.Count; i++)
            {
                var entry = ThemeDeckStableMapping.Entries[i];
                for (var seq = 1; seq <= ThemeDeckStableMapping.SequenceCount; seq++)
                {
                    var card = new CardContentDefinition(entry.GetContentId(seq), "契约" + seq, CardKind.Monster)
                        .WithSequence(seq)
                        .WithAttackPattern(AttackPattern.OrthogonalMelee)
                        .InDeck(entry.DeckId);
                    catalog.AddCard(seq == 5 ? card.AsBoss() : card);
                }

                catalog.AddMonsterDeck(new MonsterDeckDefinition(entry.DeckId, "契约卡组", MonsterDeckKind.Unknown));
            }

            if (customize != null)
            {
                customize(catalog);
            }

            return catalog;
        }

        private static void AssertAreEqualFiles(string first, string second, string message)
        {
            Assert.IsTrue(File.Exists(first), "文件不存在：" + first);
            Assert.IsTrue(File.Exists(second), "文件不存在：" + second);
            Assert.AreEqual(File.ReadAllBytes(first), File.ReadAllBytes(second), message);
        }

        private static bool HasCode(IReadOnlyList<ThemeDeckFormalReadiness.Issue> blockers, string code)
        {
            for (var i = 0; i < blockers.Count; i++)
            {
                if (string.Equals(blockers[i].Code, code, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCode(IReadOnlyList<ThemeDeckNarrowContract.Issue> issues, string code)
        {
            for (var i = 0; i < issues.Count; i++)
            {
                if (string.Equals(issues[i].Code, code, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatIssues(IReadOnlyList<ThemeDeckNarrowContract.Issue> issues)
        {
            if (issues.Count == 0)
            {
                return "no issues";
            }

            var parts = new List<string>();
            for (var i = 0; i < issues.Count; i++)
            {
                parts.Add(issues[i].Code + ": " + issues[i].Message);
            }

            return string.Join(" | ", parts);
        }

        private static string FormatBlockers(ThemeDeckFormalReadiness.Report report)
        {
            if (report.Blockers.Count == 0)
            {
                return "no blockers";
            }

            var parts = new List<string>();
            for (var i = 0; i < report.Blockers.Count; i++)
            {
                parts.Add(report.Blockers[i].Code + ": " + report.Blockers[i].Message);
            }

            return string.Join(" | ", parts);
        }
    }
}
