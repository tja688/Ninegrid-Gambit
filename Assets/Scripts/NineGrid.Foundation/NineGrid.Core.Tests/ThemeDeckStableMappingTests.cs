using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #127 / ADR-0029 内容护栏：七套稳定 ID—策划槽位—sequence 映射。
    /// 只断言稳定 ID / 槽位 / sequence / Boss / Reserve；禁止断言可变 displayName
    /// （见 ContentDisplayNameAssertGuardrailTests）。
    /// </summary>
    public sealed class ThemeDeckStableMappingTests
    {
        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            MonsterDeckTableCatalog.Invalidate();
        }

        [Test]
        public void StableMapping_SevenDecksEachFiveSlots_Unique()
        {
            Assert.AreEqual(7, ThemeDeckStableMapping.Entries.Count, "七套");
            var deckIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var contentIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < ThemeDeckStableMapping.Entries.Count; i++)
            {
                var entry = ThemeDeckStableMapping.Entries[i];
                Assert.IsTrue(deckIds.Add(entry.DeckId), "deckId 重复：" + entry.DeckId);
                Assert.AreEqual(
                    ThemeDeckStableMapping.SequenceCount,
                    entry.SequenceContentIds.Count,
                    entry.DeckId + " 槽位数");
                for (var s = 0; s < entry.SequenceContentIds.Count; s++)
                {
                    var contentId = entry.SequenceContentIds[s];
                    Assert.IsFalse(
                        string.IsNullOrWhiteSpace(contentId),
                        entry.DeckId + " 第 " + (s + 1) + " 槽为空");
                    Assert.IsTrue(
                        contentIds.Add(contentId),
                        "contentId 跨套重复：" + contentId);
                }
            }
        }

        [Test]
        public void ProductionJson_SlotAssignments_MatchStableMapping()
        {
            var catalog = ContentCatalogBootstrap.Load();
            var issues = ThemeDeckMappingVerifier.VerifySlotMapping(catalog);
            Assert.AreEqual(0, issues.Count, FormatIssues(issues));
        }

        [Test]
        public void ProductionJson_SequenceFiveIsBossAndOnlySequenceFive()
        {
            var catalog = ContentCatalogBootstrap.Load();
            var issues = ThemeDeckMappingVerifier.VerifyBossFlags(catalog);
            Assert.AreEqual(0, issues.Count, FormatIssues(issues));
        }

        [Test]
        public void ProductionJson_MappingIsCorrect_ButNotFormallyReachable()
        {
            var catalog = ContentCatalogBootstrap.Load();
            Assert.AreEqual(
                0,
                ThemeDeckMappingVerifier.VerifySlotMapping(catalog).Count,
                "映射正确层不得有槽位问题");
            Assert.AreEqual(
                0,
                ThemeDeckMappingVerifier.VerifyBossFlags(catalog).Count,
                "映射正确层不得有 Boss 问题");
            var report = ThemeDeckFormalReadiness.Evaluate(catalog);
            Assert.IsFalse(
                report.Reachable,
                "七套尚未正式启用（全表 Reserve + 过渡组未清空），正式可达性必须为否");
        }

        [Test]
        public void FormalReadiness_ReportsCurrentBlockers()
        {
            var catalog = ContentCatalogBootstrap.Load();
            var report = ThemeDeckFormalReadiness.Evaluate(catalog);

            Assert.IsFalse(report.Reachable);
            Assert.IsTrue(HasCode(report.Blockers, "deck_reserve"), "应报 deck_reserve（七套全表 Reserve）");
            Assert.IsTrue(HasCode(report.Blockers, "slot_card_reserve"), "应报 slot_card_reserve（deck.dragon seq2 稳定槽位卡为 Reserve）");
            Assert.IsTrue(HasCode(report.Blockers, "transition_not_cleared"), "应报 transition_not_cleared（过渡组未清空）");
        }

        [Test]
        public void VerifyDeck_ReportsDuplicateSequence_AndGap()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(MonsterCard("monster.melee_3", 1, false));
            catalog.AddCard(MonsterCard("monster.big_skeleton_reborn", 1, false));
            catalog.AddCard(MonsterCard("monster.headless_skeleton", 3, false));
            catalog.AddCard(MonsterCard("monster.skull_head", 4, false));
            catalog.AddCard(MonsterCard("monster.beggar", 5, true));

            var issues = ThemeDeckMappingVerifier.VerifyDeck(catalog, "deck.dragon");
            Assert.IsTrue(HasCode(issues, "slot_duplicate"), "应报 sequence 重复：\n" + FormatIssues(issues));
            Assert.IsTrue(HasCode(issues, "slot_gap"), "应报缺槽（seq2 被挤占）：\n" + FormatIssues(issues));
        }

        [Test]
        public void VerifyDeck_ReportsMissingSlot()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(MonsterCard("monster.melee_3", 1, false));
            catalog.AddCard(MonsterCard("monster.big_skeleton_reborn", 2, false));
            catalog.AddCard(MonsterCard("monster.skull_head", 4, false));
            catalog.AddCard(MonsterCard("monster.beggar", 5, true));

            var issues = ThemeDeckMappingVerifier.VerifyDeck(catalog, "deck.dragon");
            Assert.IsTrue(HasCode(issues, "slot_gap"), "应报缺槽：\n" + FormatIssues(issues));
        }

        [Test]
        public void VerifyDeck_ReportsSlotMismatch()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(MonsterCard("monster.melee_3", 1, false));
            catalog.AddCard(MonsterCard("monster.big_skeleton_reborn", 2, false));
            catalog.AddCard(MonsterCard("monster.foreign_monster", 3, false));
            catalog.AddCard(MonsterCard("monster.skull_head", 4, false));
            catalog.AddCard(MonsterCard("monster.beggar", 5, true));

            var issues = ThemeDeckMappingVerifier.VerifyDeck(catalog, "deck.dragon");
            Assert.IsTrue(HasCode(issues, "slot_mismatch"), "应报槽位错位：\n" + FormatIssues(issues));
        }

        [Test]
        public void VerifyDeck_ReportsOutOfRangeMember()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(MonsterCard("monster.melee_3", 1, false));
            catalog.AddCard(MonsterCard("monster.big_skeleton_reborn", 2, false));
            catalog.AddCard(MonsterCard("monster.headless_skeleton", 3, false));
            catalog.AddCard(MonsterCard("monster.skull_head", 4, false));
            catalog.AddCard(MonsterCard("monster.beggar", 5, true));
            catalog.AddCard(MonsterCard("monster.unassigned_orphan", 0, false));

            var issues = ThemeDeckMappingVerifier.VerifyDeck(catalog, "deck.dragon");
            Assert.IsTrue(HasCode(issues, "member_out_of_range"), "应报越界成员：\n" + FormatIssues(issues));
        }

        [Test]
        public void VerifyDeck_ReportsWrongBossFlags()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(MonsterCard("monster.melee_3", 1, true));
            catalog.AddCard(MonsterCard("monster.big_skeleton_reborn", 2, false));
            catalog.AddCard(MonsterCard("monster.headless_skeleton", 3, false));
            catalog.AddCard(MonsterCard("monster.skull_head", 4, false));
            catalog.AddCard(MonsterCard("monster.beggar", 5, false));

            var issues = ThemeDeckMappingVerifier.VerifyDeck(catalog, "deck.dragon");
            Assert.IsTrue(HasCode(issues, "boss_flag"), "应报错误 Boss 标志：\n" + FormatIssues(issues));
        }

        [Test]
        public void FormalReadiness_ReportsReserveDeck_AndReserveSlotCard()
        {
            var catalog = BuildFullContractCatalog(c =>
            {
                c.AddMonsterDeck(new MonsterDeckDefinition(Dragon.DeckId, "契约卡组", MonsterDeckKind.Reserve));
                if (c.TryGetCard(Dragon.GetContentId(2), out var reservedSlot))
                {
                    reservedSlot.AsReserve();
                }
            });

            var report = ThemeDeckFormalReadiness.Evaluate(catalog);
            Assert.IsFalse(report.Reachable);
            Assert.IsTrue(HasCode(report.Blockers, "deck_reserve"), "应报 deck_reserve：\n" + FormatBlockers(report));
            Assert.IsTrue(HasCode(report.Blockers, "slot_card_reserve"), "应报 slot_card_reserve：\n" + FormatBlockers(report));
        }

        [Test]
        public void FormalReadiness_ReportsTransitionNotCleared()
        {
            var catalog = BuildFullContractCatalog(c =>
            {
                var orphan = new CardContentDefinition("monster.still_in_transition", "过渡孤儿", CardKind.Monster)
                    .WithSequence(3)
                    .WithAttackPattern(AttackPattern.OrthogonalMelee)
                    .InDeck(ThemeDeckFormalReadiness.TransitionDeckId);
                c.AddCard(orphan);
                c.AddMonsterDeck(new MonsterDeckDefinition(
                    ThemeDeckFormalReadiness.TransitionDeckId,
                    "过渡卡组",
                    MonsterDeckKind.Unknown));
            });

            var report = ThemeDeckFormalReadiness.Evaluate(catalog);
            Assert.IsFalse(report.Reachable);
            Assert.IsTrue(HasCode(report.Blockers, "transition_not_cleared"), "应报 transition_not_cleared：\n" + FormatBlockers(report));
        }

        [Test]
        public void FormalReadiness_IsReachable_WhenAllBlockersCleared()
        {
            var report = ThemeDeckFormalReadiness.Evaluate(BuildFullContractCatalog(null));
            Assert.IsTrue(report.Reachable, "blocker 清空后应可达：\n" + FormatBlockers(report));
        }

        private static readonly ThemeDeckStableEntry Dragon = ThemeDeckStableMapping.Entries[0];

        private static GameContentCatalog BuildFullContractCatalog(System.Action<GameContentCatalog> customize)
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

        private static CardContentDefinition MonsterCard(string contentId, int sequence, bool boss)
        {
            var card = new CardContentDefinition(contentId, "契约" + sequence, CardKind.Monster)
                .WithSequence(sequence)
                .WithAttackPattern(AttackPattern.OrthogonalMelee)
                .InDeck(Dragon.DeckId);
            return boss ? card.AsBoss() : card;
        }

        private static bool HasCode(IReadOnlyList<ThemeDeckMappingVerifier.Issue> issues, string code)
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

        private static string FormatIssues(IReadOnlyList<ThemeDeckMappingVerifier.Issue> issues)
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
