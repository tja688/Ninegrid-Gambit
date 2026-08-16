using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// ADR-0054：非正式接线内容（归档 / AI 拓展 / 过渡）不得进入正式局随机池、奖池、
    /// 开局装填或具名授予。锁死玩家截图里的倍增塔上场、以及 ShuffleRandomContent 把
    /// Catalog 当全量池扫进去的回归。
    /// </summary>
    public class FormalContentWiringLeakTests
    {
        private const string DoublingTowerId = "help.doubling_tower";
        private const string AiHelpId = "help.charge_horn";
        private const string AiRelicId = "relic.forge_bracer";
        private const string ArchiveRelicId = "relic.tower_child";

        private IArchitecture mArch;
        private GameContentCatalog mCatalog;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
            mCatalog = content.Catalog;
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void CatalogMarksKnownUnofficialCards()
        {
            Assert.IsTrue(mCatalog.TryGetCard(DoublingTowerId, out var tower) && tower != null);
            Assert.IsTrue(FormalContentWiring.IsUnofficialDeck(tower.DeckId), "倍增塔应在归档卡组");
            Assert.IsFalse(tower.IsReserve, "倍增塔 isReserve=false——旧随机池只认 Reserve 才会漏");

            Assert.IsTrue(mCatalog.TryGetCard(AiHelpId, out var aiHelp) && aiHelp != null);
            Assert.IsTrue(FormalContentWiring.IsUnofficialDeck(aiHelp.DeckId), "AI 拓展道具应非正式");
            Assert.IsFalse(aiHelp.IsReserve, "AI 拓展成员不依赖 isReserve 隔离");
        }

        [Test]
        public void ShuffleRandomHelpCardPool_ExcludesArchiveAndAiExpansion()
        {
            var pool = ShuffleRandomContentIntoDrawPileAction.FindCatalogCandidates(
                mCatalog,
                CardKind.HelpCard,
                minLevel: 0,
                maxLevel: 0,
                excludeElite: false,
                excludeBoss: false,
                excludeDeckId: string.Empty);
            AssertUnofficialAbsent(pool, "ShuffleRandomContent HelpCard");
            AssertDefIdAbsent(pool, DoublingTowerId, "ShuffleRandomContent HelpCard");
        }

        [Test]
        public void ShuffleRandomMonsterPool_ExcludesAiExpansionAndReserve()
        {
            var pool = ShuffleRandomContentIntoDrawPileAction.FindCatalogCandidates(
                mCatalog,
                CardKind.Monster,
                minLevel: 1,
                maxLevel: 3,
                excludeElite: true,
                excludeBoss: true,
                excludeDeckId: string.Empty);
            AssertUnofficialAbsent(pool, "ShuffleRandomContent Monster");
            for (var i = 0; i < pool.Count; i++)
            {
                Assert.IsFalse(pool[i].IsReserve, pool[i].DefId + " 不应作为 Reserve 进入随机怪物池");
            }
        }

        [Test]
        public void RewardPoolsAndTrapPool_ExcludeUnofficial()
        {
            foreach (var pair in mCatalog.Rewards.Pools)
            {
                var pool = pair.Value;
                if (pool == null || pool.Entries == null)
                {
                    continue;
                }

                for (var i = 0; i < pool.Entries.Count; i++)
                {
                    var entry = pool.Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.DefId))
                    {
                        continue;
                    }

                    Assert.IsFalse(
                        FormalContentWiring.IsUnofficialDefId(mCatalog, entry.DefId),
                        pool.Id + " 展开条目含非正式 " + entry.DefId);
                }
            }

            var traps = RegularTrapPool.CollectRegularTraps(mCatalog);
            AssertUnofficialAbsent(traps, "RegularTrapPool");
        }

        [Test]
        public void ProfessionSourcePool_ExcludesDoublingTower()
        {
            var player = mArch.GetModel<PlayerModel>();
            ProfessionCatalog.SeedItemGenerationRules(player, mCatalog, ProfessionCatalog.Jester);
            var source = player.ItemSourcePoolDefIds;
            CollectionAssert.DoesNotContain(source, DoublingTowerId, "职业来源池不得含倍增塔");
            CollectionAssert.DoesNotContain(source, AiHelpId, "职业来源池不得含 AI 拓展道具");
            for (var i = 0; i < source.Count; i++)
            {
                Assert.IsFalse(
                    FormalContentWiring.IsUnofficialDefId(mCatalog, source[i]),
                    "来源池含非正式 " + source[i]);
            }
        }

        [Test]
        public void SpawnAndShuffle_RejectUnofficialNamedGrants()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var before = CountByDefId(registry, DoublingTowerId);

            Run(new SpawnCardAction(
                DoublingTowerId,
                CardKind.HelpCard,
                ZoneId.DrawPile,
                SlotId.None,
                1));
            Run(new ShuffleIntoDrawPileAction(DoublingTowerId, CardKind.HelpCard, 1, false));

            Assert.AreEqual(before, CountByDefId(registry, DoublingTowerId), "具名 Spawn/洗入不得造出倍增塔");
        }

        [Test]
        public void GrantRelic_RejectsUnofficialRelics()
        {
            Run(new GrantRelicAction(AiRelicId));
            Run(new GrantRelicAction(ArchiveRelicId));
            var relics = mArch.GetModel<PlayerModel>().RelicDefIds;
            CollectionAssert.DoesNotContain((System.Collections.ICollection)relics, AiRelicId);
            CollectionAssert.DoesNotContain((System.Collections.ICollection)relics, ArchiveRelicId);
        }

        [Test]
        public void GrantRelic_StillAcceptsLiveRelic()
        {
            Run(new GrantRelicAction("relic.composite_armor"));
            CollectionAssert.Contains(
                (System.Collections.ICollection)mArch.GetModel<PlayerModel>().RelicDefIds,
                "relic.composite_armor");
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private static int CountByDefId(CardRegistry registry, string defId)
        {
            var count = 0;
            foreach (var pair in registry.Cards)
            {
                var card = pair.Value;
                if (card != null && card.DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertUnofficialAbsent(IReadOnlyList<CardContentDefinition> pool, string site)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var card = pool[i];
                if (card == null)
                {
                    continue;
                }

                Assert.IsFalse(
                    FormalContentWiring.IsUnofficialDeck(card.DeckId),
                    site + " 含非正式 " + card.DefId + " deck=" + card.DeckId);
            }
        }

        private static void AssertDefIdAbsent(IReadOnlyList<CardContentDefinition> pool, string defId, string site)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].DefId == defId)
                {
                    Assert.Fail(site + " 含 " + defId);
                }
            }
        }
    }
}
