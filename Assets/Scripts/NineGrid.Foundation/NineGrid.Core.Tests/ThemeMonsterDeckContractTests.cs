using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// ADR-0022 / #86：主题卡组按层绑定 + 序列抽卡 + 层主击杀固定注入。
    /// </summary>
    public sealed class ThemeMonsterDeckContractTests
    {
        private IArchitecture mArch;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mReward = null;
        }

        [Test]
        public void FloorThemeDeck_SameWithinFloor_DistinctAcrossThreeFloors()
        {
            var run = mArch.GetModel<RunModel>();
            var floor1 = FirstEnemyDeckId(mReward.BuildNodeDeckOptions(1, null));
            Assert.AreEqual(floor1, FirstEnemyDeckId(mReward.BuildNodeDeckOptions(2, null)));
            Assert.AreEqual(floor1, run.FloorMonsterDeckId.Value);

            run.Floor.Value = 2;
            run.NodeIndex.Value = 0;
            run.FloorMonsterDeckId.Value = string.Empty;
            var floor2 = FirstEnemyDeckId(mReward.BuildNodeDeckOptions(1, null));
            Assert.AreNotEqual(floor1, floor2);

            run.Floor.Value = 3;
            run.NodeIndex.Value = 0;
            run.FloorMonsterDeckId.Value = string.Empty;
            var floor3 = FirstEnemyDeckId(mReward.BuildNodeDeckOptions(1, null));
            Assert.AreNotEqual(floor1, floor3);
            Assert.AreNotEqual(floor2, floor3);

            Assert.AreEqual(3, run.UsedMonsterDeckIds.Count);
            CollectionAssert.Contains(run.UsedMonsterDeckIds, floor1);
            CollectionAssert.Contains(run.UsedMonsterDeckIds, floor2);
            CollectionAssert.Contains(run.UsedMonsterDeckIds, floor3);
        }

        [Test]
        public void BuildNodeDeckOptions_DrawsBySequenceCounts()
        {
            var options = mReward.BuildNodeDeckOptions(8, null);
            Assert.AreEqual(15, options.EnemyCards.Count);

            var bySeq = new Dictionary<int, int>();
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                var defId = options.EnemyCards[i].DefId;
                Assert.IsTrue(mArch.GetSystem<IContentSystem>().Catalog.TryGetCard(defId, out var card));
                if (!bySeq.ContainsKey(card.Sequence))
                {
                    bySeq[card.Sequence] = 0;
                }

                bySeq[card.Sequence]++;
            }

            Assert.AreEqual(4, bySeq[1]);
            Assert.AreEqual(4, bySeq[2]);
            Assert.AreEqual(3, bySeq[3]);
            Assert.AreEqual(3, bySeq[4]);
            Assert.AreEqual(1, bySeq[5]);
            Assert.IsTrue(options.RequireElite);
        }

        [Test]
        public void ReserveDeck_IsNeverBoundAsFloorTheme()
        {
            for (var i = 0; i < 30; i++)
            {
                NineGridArchitecture.ResetForTests();
                var arch = NineGridArchitecture.Current;
                arch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
                InitialGameFactory.Create(arch, new InitialGameOptions { Seed = (ulong)(i + 1) });
                var deckId = FirstEnemyDeckId(arch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(1, null));
                Assert.AreNotEqual("deck.npc", deckId);
            }
        }

        [Test]
        public void FloorBossKill_ShufflesOneGoldenChestAndTwoGoldCards()
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var content = mArch.GetSystem<IContentSystem>();
            var registry = mArch.GetModel<CardRegistry>();
            var boss = content.CreateDraft("monster.theme_a5").Create(registry);
            Assert.Greater(boss.Counters.Get(CoreCounterKeys.Boss), 0);

            var actions = mArch.GetSystem<ITriggerSystem>().Dispatch(new TriggerContext(
                TriggerPoint.OnKill,
                TriggerTiming.Post,
                null,
                new[]
                {
                    new CoreGameEvent(CoreEventType.CardKilled, 0, "test").WithCard(boss.Uid)
                },
                null));
            Assert.Greater(actions.Count, 0);
            for (var i = 0; i < actions.Count; i++)
            {
                pipeline.Enqueue(actions[i]);
            }

            pipeline.RunToCompletion();

            var deck = mArch.GetModel<DeckModel>();
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                var card = registry.Get(deck.DrawPileUids[i]);
                if (!counts.ContainsKey(card.DefId))
                {
                    counts[card.DefId] = 0;
                }

                counts[card.DefId]++;
            }

            Assert.AreEqual(1, counts.ContainsKey("help.golden_chest_card") ? counts["help.golden_chest_card"] : 0);
            Assert.AreEqual(2, counts.ContainsKey("help.gold_card") ? counts["help.gold_card"] : 0);
        }

        private static string FirstEnemyDeckId(NodeDeckOptions options)
        {
            Assert.IsNotNull(options);
            Assert.Greater(options.EnemyCards.Count, 0);
            var defId = options.EnemyCards[0].DefId;
            if (defId.StartsWith("monster.theme_a"))
            {
                return "deck.theme_a";
            }

            if (defId.StartsWith("monster.theme_b"))
            {
                return "deck.theme_b";
            }

            if (defId.StartsWith("monster.theme_c"))
            {
                return "deck.theme_c";
            }

            return defId;
        }

        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.AddCard(new CardContentDefinition("help.golden_chest_card", "金色宝箱卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard));

            AddThemeDeck(catalog, "deck.theme_a", "A", MonsterDeckKind.Unknown);
            AddThemeDeck(catalog, "deck.theme_b", "B", MonsterDeckKind.Unknown);
            AddThemeDeck(catalog, "deck.theme_c", "C", MonsterDeckKind.Unknown);
            catalog.AddMonsterDeck(new MonsterDeckDefinition("deck.npc", "NPC", MonsterDeckKind.Reserve)
                .AddMonster("monster.npc_reserve"));
            catalog.AddCard(new CardContentDefinition("monster.npc_reserve", "NPC", CardKind.Monster)
                .WithSequence(1)
                .InDeck("deck.npc")
                .AsReserve());

            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 1, Seq1Count = 8 });
            catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 2, Seq1Count = 6, Seq2Count = 4 });
            catalog.Rewards.AddNodeRule(new NodeDeckRule
            {
                NodeIndex = 8,
                Seq1Count = 4,
                Seq2Count = 4,
                Seq3Count = 3,
                Seq4Count = 3,
                Seq5Count = 1
            });

            return catalog;
        }

        private static void AddThemeDeck(
            GameContentCatalog catalog,
            string deckId,
            string tag,
            MonsterDeckKind kind)
        {
            var deck = new MonsterDeckDefinition(deckId, tag, kind);
            for (var seq = 1; seq <= 5; seq++)
            {
                var defId = "monster.theme_" + tag.ToLowerInvariant() + seq;
                deck.AddMonster(defId);
                var card = new CardContentDefinition(defId, tag + seq, CardKind.Monster)
                    .WithStats(4, 1, 0)
                    .WithSequence(seq)
                    .WithAttackPattern(AttackPattern.OrthogonalMelee)
                    .InDeck(deckId);
                if (seq == 5)
                {
                    card.AsBoss();
                }

                catalog.AddCard(card);
            }

            catalog.AddMonsterDeck(deck);
        }
    }
}
