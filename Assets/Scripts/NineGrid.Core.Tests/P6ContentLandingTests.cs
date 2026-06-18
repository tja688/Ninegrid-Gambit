using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P6ContentLandingTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
            var catalog = TableNineContentCatalog.CreateDefault();
            P5CatalogTestSupport.RegisterCatalog(NineGridArchitecture.Current.GetUtility<IConfigUtility>(), catalog);
            NineGridArchitecture.Current.GetSystem<IContentSystem>().Load(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void DefaultCatalogValidatesImplementedDslAndTracksLongTailAtoms()
        {
            var catalog = NineGridArchitecture.Current.GetSystem<IContentSystem>().Catalog;
            var report = NineGridArchitecture.Current.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FirstIssue(report));
            Assert.GreaterOrEqual(catalog.Cards.Count, 70);
            Assert.GreaterOrEqual(catalog.Skills.Count, 50);
            Assert.GreaterOrEqual(catalog.Relics.Count, 15);
            Assert.GreaterOrEqual(report.ImplementedEffectIds.Count, 30);
            Assert.GreaterOrEqual(report.PendingEffectIds.Count, 30);
            Assert.IsTrue(catalog.Rewards.Pools.ContainsKey("kill.elite"));
            Assert.IsTrue(catalog.Rewards.Rooms.ContainsKey(RoomKind.Shop));
            Assert.AreEqual(9, catalog.Rewards.NodeDeckRules.Count);
        }

        [Test]
        public void DefaultCatalogKeepsBatchOnePendingGateAndQuickWinsImplemented()
        {
            var catalog = NineGridArchitecture.Current.GetSystem<IContentSystem>().Catalog;
            var report = NineGridArchitecture.Current.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FirstIssue(report));
            Assert.GreaterOrEqual(report.ImplementedEffectIds.Count, 51);
            Assert.LessOrEqual(report.PendingEffectIds.Count, 73);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.HelpCard), 21);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.Relic), 3);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.PlayerSkill), 3);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.MonsterSkill), 46);

            AssertImplemented(catalog, report, "relic.vitality_amulet.max_hp");
            AssertImplemented(catalog, report, "relic.throwing_knife_bag.node_start");
            AssertImplemented(catalog, report, "relic.potion_bag.node_start");
        }

        [Test]
        public void ContentDraftAppliesMonsterStatsAndActivatesImplementedSkillDsl()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var statSystem = architecture.GetSystem<IStatSystem>();

            var cub = content.CreateDraft("monster.wandering_child").Create(registry);
            board.PlaceCard(cub, SlotId.Board(6));
            content.ApplyContentToCard(cub);

            Assert.AreEqual(1, cub.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(3, cub.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(1, cub.Counters.Get(CoreCounterKeys.Level));
            Assert.IsTrue(Contains(cub.EffectIds, "skill.stray_cub.slot6"));
            Assert.AreEqual(3, statSystem.GetEffectiveInt(cub, StatId.Attack));
        }

        [Test]
        public void HelpCardUseTriggersOnlyItsOwnConfiguredDsl()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 10);

            var content = architecture.GetSystem<IContentSystem>();
            var potion = content.CreateDraft("help.healing_potion").Create(registry);
            content.ApplyContentToCard(potion);
            deck.AddToItemSlots(potion);

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(potion.Uid));

            Assert.AreEqual(20, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void EconomyPaysConfiguredGoldForAnyMonsterRemoval()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var player = architecture.GetModel<PlayerModel>();
            var content = architecture.GetSystem<IContentSystem>();
            var monster = content.CreateDraft("monster.vagrant").Create(registry);
            content.ApplyContentToCard(monster);
            board.PlaceCard(monster, SlotId.Board(2));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new RemoveCardAction(monster.Uid, ZoneId.Removed, "test"));

            Assert.AreEqual(5, player.Coins.Value);
        }

        [Test]
        public void EliteKillShufflesConfiguredRewardCardsIntoDrawPile()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var content = architecture.GetSystem<IContentSystem>();
            var elite = content.CreateDraft("monster.ringleader").Create(registry);
            elite.Stats.SetBase(StatId.MaxHp, 1);
            elite.Stats.SetBase(StatId.Hp, 1);
            content.ApplyContentToCard(elite);
            board.PlaceCard(elite, SlotId.Board(2));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, elite.Uid, 99));

            Assert.IsTrue(DeckContainsDef(deck, registry, "help.blue_chest_card"));
            Assert.IsTrue(DeckContainsDef(deck, registry, "help.gold_card"));
            Assert.IsTrue(DeckContainsDef(deck, registry, "help.stat_boost_card"));
        }

        [Test]
        public void RewardSystemBuildsNodeDeckAndResolvesRoomsFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var reward = architecture.GetSystem<IRewardSystem>();
            var options = reward.BuildNodeDeckOptions(3, "deck.wandering_legion");

            Assert.AreEqual(3, options.PlayerCards.Count);
            Assert.AreEqual(13, options.EnemyCards.Count);
            Assert.IsTrue(options.RequireElite);

            var player = architecture.GetModel<PlayerModel>();
            reward.ResolveRoom(RoomKind.Gold);
            Assert.AreEqual(50, player.Coins.Value);

            var registry = architecture.GetModel<CardRegistry>();
            var avatar = registry.Get(architecture.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 1);
            reward.ResolveRoom(RoomKind.Fountain);

            Assert.AreEqual(34, avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(34, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void VitalityAmuletCatalogDslAppliesPermanentMaxHpModifier()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);

            content.ActivateRelic("relic.vitality_amulet");

            Assert.AreEqual(36, architecture.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.MaxHp));
        }

        [Test]
        public void BagRelicsCatalogDslSpawnConfiguredHelpCardsIntoPlayerPoolOnNodeStart()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();
            var content = architecture.GetSystem<IContentSystem>();
            var deck = architecture.GetModel<DeckModel>();
            var registry = architecture.GetModel<CardRegistry>();

            player.AddRelic("relic.throwing_knife_bag");
            player.AddRelic("relic.potion_bag");
            content.ActivateRelic("relic.throwing_knife_bag");
            content.ActivateRelic("relic.potion_bag");

            architecture.GetSystem<IActionPipelineSystem>().Execute(new NodeStartedAction());

            Assert.AreEqual(4, deck.PlayerCardPoolUids.Count);
            Assert.AreEqual(2, CountCardsByDef(deck.PlayerCardPoolUids, registry, "help.throwing_knife"));
            Assert.AreEqual(2, CountCardsByDef(deck.PlayerCardPoolUids, registry, "help.healing_potion"));
        }

        private static bool Contains(IReadOnlyList<string> values, string expected)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == expected)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DeckContainsDef(DeckModel deck, CardRegistry registry, string defId)
        {
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                if (registry.Get(deck.DrawPileUids[i]).DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountEffectsByContainer(
            GameContentCatalog catalog,
            IReadOnlyList<string> effectIds,
            EffectContainerType containerType)
        {
            var count = 0;
            for (var i = 0; i < effectIds.Count; i++)
            {
                ContentEffectDefinition effect;
                if (catalog.TryGetEffect(effectIds[i], out effect) && effect.ContainerType == containerType)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountCardsByDef(IReadOnlyList<int> cardUids, CardRegistry registry, string defId)
        {
            var count = 0;
            for (var i = 0; i < cardUids.Count; i++)
            {
                if (registry.Get(cardUids[i]).DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertImplemented(GameContentCatalog catalog, ContentValidationReport report, string effectId)
        {
            ContentEffectDefinition effect;
            Assert.IsTrue(catalog.TryGetEffect(effectId, out effect), "Missing effect: " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, effect.State, effectId);
            Assert.IsTrue(Contains(report.ImplementedEffectIds, effectId), effectId);
            Assert.IsFalse(Contains(report.PendingEffectIds, effectId), effectId);
        }

        private static string FirstIssue(ContentValidationReport report)
        {
            return report.Issues.Count == 0 ? string.Empty : report.Issues[0];
        }
    }
}
