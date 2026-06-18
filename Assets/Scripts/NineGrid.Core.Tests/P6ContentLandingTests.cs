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
        public void DefaultCatalogKeepsBatchSevenPendingGateAndConvertedEffectsImplemented()
        {
            var catalog = NineGridArchitecture.Current.GetSystem<IContentSystem>().Catalog;
            var report = NineGridArchitecture.Current.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FirstIssue(report));
            Assert.GreaterOrEqual(report.ImplementedEffectIds.Count, 100);
            Assert.LessOrEqual(report.PendingEffectIds.Count, 32);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.HelpCard), 6);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.Relic), 1);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.PlayerSkill), 1);
            Assert.LessOrEqual(CountEffectsByContainer(catalog, report.PendingEffectIds, EffectContainerType.MonsterSkill), 24);

            AssertImplemented(catalog, report, "relic.vitality_amulet.max_hp");
            AssertImplemented(catalog, report, "relic.throwing_knife_bag.node_start");
            AssertImplemented(catalog, report, "relic.potion_bag.node_start");
            AssertImplemented(catalog, report, "help.fireball.use");
            AssertImplemented(catalog, report, "help.food_card.use");
            AssertImplemented(catalog, report, "help.impact_tutorial.use");
            AssertImplemented(catalog, report, "help.shield_bash_tutorial.use");
            AssertImplemented(catalog, report, "help.rotation_wheel.use");
            AssertImplemented(catalog, report, "skill.thief_claims.move");
            AssertImplemented(catalog, report, "skill.unstable.move");
            AssertImplemented(catalog, report, "skill.random_walk.move");
            AssertImplemented(catalog, report, "help.ward_magic_card.use");
            AssertImplemented(catalog, report, "skill.stray_cub.first_strike");
            AssertImplemented(catalog, report, "skill.first_strike.rule");
            AssertImplemented(catalog, report, "skill.blessing.rule");
            AssertImplemented(catalog, report, "help.doubling_tower.board_monster");
            AssertImplemented(catalog, report, "help.doubling_tower.item_player");
            AssertImplemented(catalog, report, "relic.gold_armor.rule");
            AssertImplemented(catalog, report, "skill.taunt.rule");
            AssertImplemented(catalog, report, "help.common_chest_card.use");
            AssertImplemented(catalog, report, "help.blue_chest_card.use");
            AssertImplemented(catalog, report, "help.golden_chest_card.use");
            AssertImplemented(catalog, report, "help.blood_conversion.use");
            AssertImplemented(catalog, report, "skill.easy_road.node_end");
            AssertImplemented(catalog, report, "relic.lucky_coin.elite_kill");
            AssertImplemented(catalog, report, "relic.lucky_coin.boss_kill");
            AssertImplemented(catalog, report, "skill.thorn_skin.battle");
            AssertImplemented(catalog, report, "skill.learning_growth.gain");
            AssertImplemented(catalog, report, "skill.intense_burning.flame_deal");
            AssertImplemented(catalog, report, "skill.violence_maniac.move");
            AssertImplemented(catalog, report, "skill.violence_nutrition.monster_remove");
            AssertImplemented(catalog, report, "skill.violence_nutrition.help_remove");
            AssertImplemented(catalog, report, "skill.flame_boiling.rule");
            AssertImplemented(catalog, report, "skill.absorb_bone.remove");
            AssertImplemented(catalog, report, "skill.guide.create_blessed");
            AssertImplemented(catalog, report, "skill.guide.blessed_enter");
            AssertImplemented(catalog, report, "help.swap_card.use");
            AssertImplemented(catalog, report, "help.teleport_card.use");
            AssertImplemented(catalog, report, "help.stat_boost_card.use");
            AssertImplemented(catalog, report, "help.bear_trap.use");
            AssertImplemented(catalog, report, "skill.range_expand.rule");
            AssertImplemented(catalog, report, "skill.hoodlum.slot1");
            AssertImplemented(catalog, report, "skill.fall_apart.remove");
            AssertImplemented(catalog, report, "skill.turn_world.enter");
            AssertImplemented(catalog, report, "skill.air_strike.slot1");
            AssertImplemented(catalog, report, "skill.gear_delivery.move");
            AssertImplemented(catalog, report, "skill.stone_growth.slot1");
            AssertImplemented(catalog, report, "skill.space_mastery.battle");
        }

        [Test]
        public void MonsterComposableBatchNineCatalogDslHandlesMovementAndRemovalSkills()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var avatar = architecture.GetModel<BoardModel>().AvatarUid.Value;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var avatarCard = registry.Get(avatar);
            var initialHp = avatarCard.Stats.GetBase(StatId.Hp);

            var hoodlum = PlaceMonster("monster.hoodlum", 20, SlotId.Board(2));
            content.ApplyContentToCard(hoodlum);
            pipeline.Execute(new MoveCardAction(hoodlum.Uid, SlotId.Board(1)));
            Assert.AreEqual(initialHp - 2, avatarCard.Stats.GetBase(StatId.Hp));

            var skeleton = PlaceMonster("monster.big_skeleton", 20, SlotId.Board(3));
            content.ApplyContentToCard(skeleton);
            pipeline.Execute(new RemoveCardAction(skeleton.Uid, ZoneId.Removed, "test", "test"));
            Assert.AreEqual(1, CountCardsByDef(deck.DrawPileUids, registry, "monster.skull_head"));
            Assert.AreEqual(1, CountCardsByDef(deck.DrawPileUids, registry, "monster.headless_skeleton"));
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

        [Test]
        public void BatchTwoDynamicValueHelpCardsExecuteFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 5);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 8);
            avatar.Stats.SetBase(StatId.Armor, 7);

            var fireballTarget = PlaceMonster("monster.fireball.target", 20, SlotId.Board(2));
            UseHelpCard("help.fireball");
            Assert.AreEqual(15, fireballTarget.Stats.GetBase(StatId.Hp));
            RemoveFromBoard(fireballTarget);

            var impactTarget = PlaceMonster("monster.impact.target", 20, SlotId.Board(2));
            UseHelpCard("help.impact_tutorial");
            Assert.AreEqual(12, impactTarget.Stats.GetBase(StatId.Hp));
            RemoveFromBoard(impactTarget);

            var shieldTarget = PlaceMonster("monster.shield.target", 20, SlotId.Board(2));
            UseHelpCard("help.shield_bash_tutorial");
            Assert.AreEqual(13, shieldTarget.Stats.GetBase(StatId.Hp));

            UseHelpCard("help.food_card");
            Assert.AreEqual(20, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void BatchThreeRotationWheelRotatesCounterClockwiseFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var monster = PlaceMonster("monster.rotation.target", 10, SlotId.Board(1));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "help.rotation_wheel.use",
                new EffectOwner(EffectContainerType.HelpCard, "help.rotation_wheel", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(9001));

            Assert.AreEqual(SlotId.Board(4), monster.Slot.Value);
        }

        [Test]
        public void BatchThreeThiefClaimsRemovesAdjacentHelpCardFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var thief = PlaceMonster("monster.pickpocket.test", 10, SlotId.Board(1));
            var help = PlaceHelpCard("help.thief.target", SlotId.Board(5));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.thief_claims.move",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.thief_claims", thief.Uid));

            MoveThreeTimes(thief, SlotId.Board(4), SlotId.Board(1), SlotId.Board(4));

            Assert.AreEqual(ZoneId.Removed, help.Zone.Value);
        }

        [Test]
        public void BatchThreeRandomMovementSkillsSwapFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var unstable = PlaceMonster("monster.unstable.test", 10, SlotId.Board(1));
            PlaceMonster("monster.unstable.a", 10, SlotId.Board(2));
            PlaceMonster("monster.unstable.b", 10, SlotId.Board(3));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.unstable.move",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.unstable", unstable.Uid));

            MoveThreeTimes(unstable, SlotId.Board(4), SlotId.Board(1), SlotId.Board(4));

            Assert.AreNotEqual(SlotId.Board(4), unstable.Slot.Value);
            Assert.IsTrue(HasEvent(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.CardSwapped));
        }

        [Test]
        public void BatchThreeRandomWalkSwapsWithHelpCardFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var walker = PlaceMonster("monster.random.walk.test", 10, SlotId.Board(1));
            var help = PlaceHelpCard("help.random.walk.target", SlotId.Board(5));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.random_walk.move",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.random_walk", walker.Uid));

            MoveThreeTimes(walker, SlotId.Board(4), SlotId.Board(1), SlotId.Board(4));

            Assert.AreEqual(SlotId.Board(5), walker.Slot.Value);
            Assert.AreEqual(SlotId.Board(4), help.Slot.Value);
        }

        [Test]
        public void BatchFourWardMagicPreventsOnlyNextPlayerDamageFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var monster = PlaceMonster("monster.ward.attacker", 20, SlotId.Board(2));

            UseHelpCard("help.ward_magic_card");
            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(monster.Uid, avatar.Uid, 8));
            Assert.AreEqual(30, avatar.Stats.GetBase(StatId.Hp));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(monster.Uid, avatar.Uid, 8));
            Assert.AreEqual(22, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void BatchFourFirstStrikeAndBlessingCatalogRulesApplyToOwner()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var statSystem = architecture.GetSystem<IStatSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 5);
            var cub = PlaceMonster("monster.wandering_child", 10, SlotId.Board(6));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.stray_cub.first_strike",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.stray_cub", cub.Uid));

            Assert.AreEqual(1f, statSystem.EvaluateRule(RuleId.FirstStrike, 0f, statSystem.CreateContext(cub)));
            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(cub.Uid, SlotId.Board(4)));
            Assert.AreEqual(0f, statSystem.EvaluateRule(RuleId.FirstStrike, 0f, statSystem.CreateContext(cub)));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.blessing.rule",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.blessing", cub.Uid));
            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, cub.Uid, 5));
            Assert.AreEqual(10, cub.Stats.GetBase(StatId.Hp));
            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, cub.Uid, 5));
            Assert.AreEqual(5, cub.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void BatchFourContinuationCatalogRulesCoverTauntGoldArmorAndDoublingTower()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var player = architecture.GetModel<PlayerModel>();
            var avatar = registry.Get(architecture.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Armor, 3);
            player.AddCoins(15);
            content.ActivateRelic("relic.gold_armor");

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(0, avatar.Uid, 4));
            Assert.AreEqual(0, player.Coins.Value);
            Assert.AreEqual(3, avatar.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(29, avatar.Stats.GetBase(StatId.Hp));

            var taunter = PlaceMonster("monster.skeleton_taunter", 7, SlotId.Board(2));
            var other = PlaceMonster("monster.taunt.other", 10, SlotId.Board(4));
            content.ApplyContentToCard(taunter);
            architecture.GetSystem<IActionPipelineSystem>().Execute(new ChangePhaseAction(GamePhase.InteractionLoop));
            Assert.IsFalse(architecture.GetSystem<IPhaseSystem>().Attack(other.Slot.Value).Accepted);
            RemoveFromBoard(taunter);
            RemoveFromBoard(other);

            var tower = content.CreateDraft("help.doubling_tower").Create(registry);
            architecture.GetModel<BoardModel>().PlaceCard(tower, SlotId.Board(1));
            content.ApplyContentToCard(tower);
            var knife = content.CreateDraft("help.throwing_knife").Create(registry);
            content.ApplyContentToCard(knife);
            var target = PlaceMonster("monster.doubling.catalog", 20, SlotId.Board(8));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(knife.Uid, new[] { target.Uid }));

            Assert.AreEqual(8, target.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void BatchFiveChestAndEasyRoadCatalogDslOfferRewardChoices()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();

            UseHelpCard("help.common_chest_card");
            AssertRewardOffered("relic.common_chest", 3, "relic.wood_shield");

            UseHelpCard("help.blue_chest_card");
            AssertRewardOffered("relic.blue_chest", 3, "relic.vitality_amulet");

            UseHelpCard("help.golden_chest_card");
            AssertRewardOffered("relic.golden_chest", 3, "relic.phoenix_feather");

            content.ActivatePlayerSkill("skill.easy_road");
            architecture.GetSystem<IActionPipelineSystem>().Execute(new NodeCompletedAction());

            AssertRewardOffered("help.white.choice", 3, "HelpCard");
        }

        [Test]
        public void BatchFiveLuckyCoinCatalogDslAddsGoldCardForEliteKill()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var content = architecture.GetSystem<IContentSystem>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var elite = content.CreateDraft("monster.ringleader").Create(registry);
            elite.Stats.SetBase(StatId.MaxHp, 1);
            elite.Stats.SetBase(StatId.Hp, 1);
            content.ApplyContentToCard(elite);
            board.PlaceCard(elite, SlotId.Board(2));

            content.ActivateRelic("relic.lucky_coin");
            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, elite.Uid, 99));

            Assert.GreaterOrEqual(CountCardsByDef(deck.DrawPileUids, registry, "help.gold_card"), 2);
        }

        [Test]
        public void BatchFiveBloodConversionCatalogDslSpendsMaxHpAndRollsReward()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 30);
            avatar.Stats.SetBase(StatId.Hp, 30);
            avatar.Stats.SetBase(StatId.Attack, 3);
            avatar.Stats.SetBase(StatId.Armor, 2);
            architecture.GetUtility<IRngUtility>().SetSeed(1UL);

            UseHelpCard("help.blood_conversion");

            Assert.AreEqual(25, avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(25, avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(4, avatar.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(2, avatar.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(0, architecture.GetModel<PlayerModel>().Coins.Value);
            Assert.IsTrue(HasEvent(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.BaseStatModified));
        }

        [Test]
        public void BatchFiveCSelectedHelpCardsExecuteFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 10);

            UseHelpCard("help.stat_boost_card", null, "Attack");
            Assert.AreEqual(2, avatar.Stats.GetBase(StatId.Attack));

            UseHelpCard("help.stat_boost_card", null, "Armor");
            Assert.AreEqual(1, avatar.Stats.GetBase(StatId.Armor));

            UseHelpCard("help.stat_boost_card", null, "Hp");
            Assert.AreEqual(22, avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(12, avatar.Stats.GetBase(StatId.Hp));

            var first = PlaceMonster("monster.swap.first", 10, SlotId.Board(1));
            var second = PlaceHelpCard("help.swap.second", SlotId.Board(2));
            UseHelpCard("help.swap_card", new[] { first.Uid, second.Uid }, null);
            Assert.AreEqual(SlotId.Board(2), first.Slot.Value);
            Assert.AreEqual(SlotId.Board(1), second.Slot.Value);

            var teleported = PlaceMonster("monster.teleport.target", 10, SlotId.Board(3));
            UseHelpCard("help.teleport_card", new[] { teleported.Uid }, null);
            Assert.AreEqual(ZoneId.DrawPile, teleported.Zone.Value);
            Assert.AreEqual(SlotId.None, teleported.Slot.Value);
            Assert.IsTrue(ContainsUid(deck.DrawPileUids, teleported.Uid));
            Assert.IsTrue(HasEvent(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.CardDealt));
        }

        [Test]
        public void BatchSixThornSkinCatalogDslDealsTargetAttackDamageOnce()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var monster = PlaceMonster("monster.thorn.catalog", 20, SlotId.Board(2));
            monster.Stats.SetBase(StatId.Attack, 4);

            architecture.GetSystem<IContentSystem>().ActivatePlayerSkill("skill.thorn_skin");
            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, monster.Uid, 1));

            Assert.AreEqual(15, monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(1, CountEvents(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.EffectTriggered));
        }

        [Test]
        public void BatchSixBLearningGrowthCatalogDslGainsAttackFromOtherMonsterOnly()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var learner = PlaceMonster("monster.young_orc", 20, SlotId.Board(1));
            content.ApplyContentToCard(learner);
            var other = PlaceMonster("monster.brainless_orc", 20, SlotId.Board(2));
            content.ApplyContentToCard(other);
            var initialAttack = learner.Stats.GetBase(StatId.Attack);

            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new ModifyBaseStatAction(other.Uid, StatId.Attack, 1, "test.other.buff", "skill.test_buff"));

            Assert.AreEqual(initialAttack + 1, learner.Stats.GetBase(StatId.Attack));

            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new ModifyBaseStatAction(learner.Uid, StatId.Attack, 1, "test.self.buff", "skill.test_buff"));

            Assert.AreEqual(initialAttack + 2, learner.Stats.GetBase(StatId.Attack));
        }

        [Test]
        public void BatchSixBIntenseBurningCatalogDslDuplicatesFlameWithoutRecursing()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var owner = PlaceMonster("monster.fire_cult_leader", 20, SlotId.Board(1));
            content.ApplyContentToCard(owner);
            var deck = architecture.GetModel<DeckModel>();
            var registry = architecture.GetModel<CardRegistry>();

            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new ShuffleIntoDrawPileAction("help.flame", CardKind.HelpCard, 1, false, "skill.devotion"));

            Assert.AreEqual(2, CountCardsByDef(deck.DrawPileUids, registry, "help.flame"));
            Assert.AreEqual(1, CountEvents(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.EffectTriggered));
        }

        [Test]
        public void BatchSixBViolenceManiacCatalogDslFeedsViolenceNutritionBySource()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var owner = PlaceMonster("monster.orc_boss", 20, SlotId.Board(5));
            content.ApplyContentToCard(owner);
            var initialAttack = owner.Stats.GetBase(StatId.Attack);
            var initialArmor = owner.Stats.GetBase(StatId.Armor);
            var monster = PlaceMonster("monster.veteran.orc.target", 20, SlotId.Board(2));
            var help = PlaceHelpCard("help.violence.target", SlotId.Board(4));

            MoveThreeTimes(owner, SlotId.Board(8), SlotId.Board(5), SlotId.Board(8));

            Assert.AreEqual(ZoneId.Removed, monster.Zone.Value);
            Assert.AreEqual(ZoneId.Removed, help.Zone.Value);
            Assert.AreEqual(initialAttack + 3, owner.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(initialArmor + 5, owner.Stats.GetBase(StatId.Armor));
        }

        [Test]
        public void BatchSixTailFlameBoilingCatalogDslAddsOnlyFlameDamage()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var avatar = architecture.GetModel<CardRegistry>().Get(architecture.GetModel<BoardModel>().AvatarUid.Value);
            var dragon = PlaceMonster("monster.fire_dragon", 50, SlotId.Board(1));
            content.ApplyContentToCard(dragon);
            var flameTarget = PlaceMonster("monster.flame.target", 20, SlotId.Board(2));
            var normalTarget = PlaceMonster("monster.normal.target", 20, SlotId.Board(3));

            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new DealDamageAction(avatar.Uid, flameTarget.Uid, 2, "help.flame"));
            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new DealDamageAction(avatar.Uid, normalTarget.Uid, 2, "help.fireball"));

            Assert.AreEqual(17, flameTarget.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(18, normalTarget.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void BatchSixTailAbsorbBoneCatalogDslUsesRemovedAdjacentMonsterSnapshot()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var owner = PlaceMonster("monster.skeleton_king", 30, SlotId.Board(5));
            content.ApplyContentToCard(owner);
            var initialAttack = owner.Stats.GetBase(StatId.Attack);
            var initialArmor = owner.Stats.GetBase(StatId.Armor);
            var adjacent = PlaceMonster("monster.bone.adjacent", 10, SlotId.Board(2));
            adjacent.Stats.SetBase(StatId.Attack, 4);
            adjacent.Stats.SetBase(StatId.Armor, 6);
            var diagonal = PlaceMonster("monster.bone.diagonal", 10, SlotId.Board(9));
            diagonal.Stats.SetBase(StatId.Attack, 7);
            diagonal.Stats.SetBase(StatId.Armor, 8);

            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new RemoveCardAction(adjacent.Uid, ZoneId.Removed, "test.adjacent"));
            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new RemoveCardAction(diagonal.Uid, ZoneId.Removed, "test.diagonal"));

            Assert.AreEqual(initialAttack + 4, owner.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(initialArmor + 6, owner.Stats.GetBase(StatId.Armor));
        }

        [Test]
        public void BatchSevenGuideCatalogDslCreatesBlessedSlotAndRewardsMonsterEntry()
        {
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IRngUtility>().SetSeed(20260618UL);
            var content = architecture.GetSystem<IContentSystem>();
            var board = architecture.GetModel<BoardModel>();
            var guide = PlaceMonster("monster.ringleader", 20, SlotId.Board(1));
            content.ApplyContentToCard(guide);
            var expected = ExpectedBlessedSlot(20260618UL);

            MoveThreeTimes(guide, SlotId.Board(2), SlotId.Board(1), SlotId.Board(2));

            Assert.IsTrue(board.IsBlessed(expected));
            Assert.AreEqual(1, board.CountMarkedSlots(BoardMarkId.Blessed));

            var mover = PlaceMonster("monster.guide.reward.target", 20, SlotId.Board(4));
            mover.Stats.SetBase(StatId.Attack, 2);
            mover.Stats.SetBase(StatId.Armor, 3);
            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new SetBoardMarkAction(SlotId.Board(3), BoardMarkId.Blessed, true, "test", "setup"));
            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(mover.Uid, SlotId.Board(3)));

            Assert.AreEqual(5, mover.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(3, mover.Stats.GetBase(StatId.Attack));
            Assert.IsTrue(HasEvent(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.BoardMarked));
        }

        [Test]
        public void BatchSevenBBearTrapCatalogDslDamagesAdjacentRefillMonsterThenRemovesSelf()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var boardSystem = architecture.GetSystem<IBoardSystem>();

            var trap = content.CreateDraft("help.bear_trap").Create(registry);
            architecture.GetModel<BoardModel>().PlaceCard(trap, SlotId.Board(1));
            content.ApplyContentToCard(trap);

            var monster = registry.Create("monster.bear.trap.target", CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 15);
            monster.Stats.SetBase(StatId.Hp, 15);
            deck.AddToDrawPile(monster, false);

            boardSystem.FillEmptySlots();

            Assert.AreEqual(SlotId.Board(2), monster.Slot.Value);
            Assert.AreEqual(5, monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(ZoneId.Removed, trap.Zone.Value);
            Assert.IsFalse(Contains(trap.EffectIds, "help.bear_trap.use"));
            Assert.IsTrue(HasEvent(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.EffectTriggered));
        }

        [Test]
        public void BatchSevenRangeExpandCatalogDslMakesSkeletonMageVirtuallyAdjacentToMonsters()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var boardSystem = architecture.GetSystem<IBoardSystem>();
            var mage = PlaceMonster("monster.skeleton_mage", 20, SlotId.Board(1));
            var farMonster = PlaceMonster("monster.range.target", 20, SlotId.Board(9));
            var help = PlaceHelpCard("help.range.helper", SlotId.Board(7));

            content.ApplyContentToCard(mage);

            Assert.IsTrue(Contains(mage.EffectIds, "skill.range_expand.rule"));
            Assert.IsTrue(boardSystem.AreAdjacent(mage.Slot.Value, farMonster.Slot.Value));
            Assert.IsFalse(boardSystem.AreAdjacent(mage.Slot.Value, help.Slot.Value));
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

        private static bool ContainsUid(IReadOnlyList<int> values, int uid)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == uid)
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

        private static CardInstance PlaceMonster(string defId, int hp, SlotId slot)
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            architecture.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private static CardInstance PlaceHelpCard(string defId, SlotId slot)
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var card = registry.Create(defId, CardKind.HelpCard);
            architecture.GetModel<BoardModel>().PlaceCard(card, slot);
            return card;
        }

        private static void MoveThreeTimes(CardInstance card, SlotId first, SlotId second, SlotId third)
        {
            var pipeline = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new MoveCardAction(card.Uid, first));
            pipeline.Execute(new MoveCardAction(card.Uid, second));
            pipeline.Execute(new MoveCardAction(card.Uid, third));
        }

        private static void UseHelpCard(string defId)
        {
            UseHelpCard(defId, null, null);
        }

        private static void UseHelpCard(string defId, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var card = content.CreateDraft(defId).Create(registry);
            content.ApplyContentToCard(card);
            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(card.Uid, selectedCardUids, selectedOption));
        }

        private static void RemoveFromBoard(CardInstance card)
        {
            NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>().Execute(
                new RemoveCardAction(card.Uid, ZoneId.Removed, "test"));
        }

        private static void AssertImplemented(GameContentCatalog catalog, ContentValidationReport report, string effectId)
        {
            ContentEffectDefinition effect;
            Assert.IsTrue(catalog.TryGetEffect(effectId, out effect), "Missing effect: " + effectId);
            Assert.AreEqual(ContentImplementationState.Implemented, effect.State, effectId);
            Assert.IsTrue(Contains(report.ImplementedEffectIds, effectId), effectId);
            Assert.IsFalse(Contains(report.PendingEffectIds, effectId), effectId);
        }

        private static bool HasEvent(EventLog eventLog, CoreEventType type)
        {
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                if (eventLog.Entries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountEvents(EventLog eventLog, CoreEventType type)
        {
            var count = 0;
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                if (eventLog.Entries[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertRewardOffered(string poolId, int amount, string expectedDefId)
        {
            var eventLog = NineGridArchitecture.Current.GetSystem<IActionPipelineSystem>().EventLog;
            for (var i = eventLog.Entries.Count - 1; i >= 0; i--)
            {
                var entry = eventLog.Entries[i];
                if (entry.Type != CoreEventType.RewardOffered || !entry.Message.StartsWith(poolId))
                {
                    continue;
                }

                Assert.AreEqual(amount, entry.Amount);
                Assert.IsTrue(entry.Message.Contains(expectedDefId), entry.Message);
                return;
            }

            Assert.Fail("Missing reward offer for pool: " + poolId);
        }

        private static SlotId ExpectedBlessedSlot(ulong seed)
        {
            var candidates = new[] { 1, 2, 3, 4, 6, 7, 8, 9 };
            var index = new DeterministicRngUtility(seed).Range(0, candidates.Length);
            return SlotId.Board(candidates[index]);
        }

        private static string FirstIssue(ContentValidationReport report)
        {
            return report.Issues.Count == 0 ? string.Empty : report.Issues[0];
        }
    }
}
