using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P5EffectSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
            P5CatalogTestSupport.RegisterCatalog(NineGridArchitecture.Current);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AtomRegistryDiscoversCoreP5AtomsAndValidatorReportsFiveDefenseIssues()
        {
            var effectSystem = Effects();
            Assert.IsTrue(effectSystem.AtomRegistry.Triggers.ContainsKey("OnFatalDamage"));
            Assert.IsTrue(effectSystem.AtomRegistry.Triggers.ContainsKey("OnEvent"));
            Assert.IsTrue(effectSystem.AtomRegistry.Triggers.ContainsKey("OnDeal"));
            Assert.IsTrue(effectSystem.AtomRegistry.Triggers.ContainsKey("OnMoveToBoardMark"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("OwnsRelicSet"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("AdjacentHasCard"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("EventFilter"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("ActionSource"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("SelectedOption"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("CardZone"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("RandomMonster"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("FilteredCards"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("AdjacentCard"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("BoardMarkEventCard"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("SelectedCards"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("WeightedRandom"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("CardCounter"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("EventTarget"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("OfferRewardChoice"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("ModifyBaseStat"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("GrantRewardFromPool"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("GrantRelic"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("GrantPlayerSkillContent"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("MoveToDrawPile"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("SetBoardMark"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("ReplayHelpCardEffects"));

            var invalid = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.relic\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Triggered\","
                + "\"verb\":\"Use\","
                + "\"modifier\":{\"stat\":\"Attack\",\"value\":1}"
                + "}");

            var validation = effectSystem.Validate(invalid);
            Assert.IsFalse(validation.IsValid);
            AssertHasIssue(validation, "typeTag.mismatch");
            AssertHasIssue(validation, "required.trigger");
            AssertHasIssue(validation, "required.action");
            AssertHasIssue(validation, "exclusive.modifier");
            AssertHasIssue(validation, "verb.relic");
            Assert.IsTrue(validation.GoldenSnapshot.Contains("bad.relic|Triggered|Relic"));
        }

        [Test]
        public void ValidatorRejectsUnknownAtomMissingSchemaAndOutOfRangeFields()
        {
            var effectSystem = Effects();

            var unknownAtom = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.unknown\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnFakeTrigger\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"DealDamage\",\"amount\":-1}"
                + "}");
            var unknownValidation = effectSystem.Validate(unknownAtom);
            Assert.IsFalse(unknownValidation.IsValid);
            AssertHasIssue(unknownValidation, "atom.unknown");
            AssertHasIssue(unknownValidation, "schema.range.amount");

            var missingFields = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.missing\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnCumulative\"},"
                + "\"conditions\":[{\"atom\":\"AdjacentHasCard\"}],"
                + "\"target\":{\"atom\":\"AdjacentCard\"},"
                + "\"action\":{\"atom\":\"ShuffleInto\"}"
                + "}");
            var missingValidation = effectSystem.Validate(missingFields);
            Assert.IsFalse(missingValidation.IsValid);
            AssertHasIssue(missingValidation, "schema.trigger.metric");
            AssertHasIssue(missingValidation, "schema.trigger.threshold");
            AssertHasIssue(missingValidation, "schema.condition.defId");
            AssertHasIssue(missingValidation, "schema.target.defId");
            AssertHasIssue(missingValidation, "schema.action.defId");

            var badValue = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.value\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"GainArmor\",\"value\":{\"source\":\"Nowhere\",\"stat\":\"Bogus\"}}"
                + "}");
            var valueValidation = effectSystem.Validate(badValue);
            Assert.IsFalse(valueValidation.IsValid);
            AssertHasIssue(valueValidation, "schema.value.source");

            var badMovement = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.movement\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"FilteredCards\",\"count\":-1},"
                + "\"action\":{\"atom\":\"Rotate\",\"direction\":\"Sideways\"}"
                + "}");
            var movementValidation = effectSystem.Validate(badMovement);
            Assert.IsFalse(movementValidation.IsValid);
            AssertHasIssue(movementValidation, "schema.range.count");
            AssertHasIssue(movementValidation, "schema.rotate.direction");

            var badSelection = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.selection\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"conditions\":[{\"atom\":\"SelectedOption\"}],"
                + "\"target\":{\"atom\":\"SelectedCards\",\"count\":-1,\"kind\":\"Ghost\",\"zone\":\"Nowhere\"},"
                + "\"action\":{\"atom\":\"Swap\"}"
                + "}");
            var selectionValidation = effectSystem.Validate(badSelection);
            Assert.IsFalse(selectionValidation.IsValid);
            AssertHasIssue(selectionValidation, "schema.condition.option");
            AssertHasIssue(selectionValidation, "schema.range.count");
            AssertHasIssue(selectionValidation, "schema.target.kind");
            AssertHasIssue(selectionValidation, "schema.target.zone");

            var badReward = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.reward\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnKill\"},"
                + "\"conditions\":[{\"atom\":\"CardCounter\",\"min\":0}],"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"OfferRewardChoice\"}"
                + "}");
            var rewardValidation = effectSystem.Validate(badReward);
            Assert.IsFalse(rewardValidation.IsValid);
            AssertHasIssue(rewardValidation, "schema.condition.key");
            AssertHasIssue(rewardValidation, "schema.range.min");
            AssertHasIssue(rewardValidation, "schema.action.poolId");

            var badContentActions = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.content.actions\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Sequence\",\"actions\":["
                + "{\"atom\":\"ModifyBaseStat\"},"
                + "{\"atom\":\"GrantRewardFromPool\"},"
                + "{\"atom\":\"GrantRelic\"},"
                + "{\"atom\":\"GrantPlayerSkillContent\"}"
                + "]}"
                + "}");
            var contentActionValidation = effectSystem.Validate(badContentActions);
            Assert.IsFalse(contentActionValidation.IsValid);
            AssertHasIssue(contentActionValidation, "schema.action.stat");
            AssertHasIssue(contentActionValidation, "schema.action.delta");
            AssertHasIssue(contentActionValidation, "schema.action.poolId");
            AssertHasIssue(contentActionValidation, "schema.action.relicDefId");
            AssertHasIssue(contentActionValidation, "schema.action.skillDefId");

            var badEventFilter = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.event.filter\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnEvent\",\"eventType\":\"Nope\"},"
                + "\"conditions\":[{\"atom\":\"EventFilter\",\"eventTypes\":[\"BaseStatModified\",\"Nope\"],\"targetKind\":\"Ghost\",\"stat\":\"Bogus\"}],"
                + "\"target\":{\"atom\":\"Self\"},"
                + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1}"
                + "}");
            var eventFilterValidation = effectSystem.Validate(badEventFilter);
            Assert.IsFalse(eventFilterValidation.IsValid);
            AssertHasIssue(eventFilterValidation, "schema.trigger.eventType");
            AssertHasIssue(eventFilterValidation, "schema.condition.eventType");
            AssertHasIssue(eventFilterValidation, "schema.condition.targetKind");
            AssertHasIssue(eventFilterValidation, "schema.condition.stat");

            var badBoardMark = effectSystem.ParseJson(
                "{"
                + "\"id\":\"bad.board.mark\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnMoveToBoardMark\",\"mark\":\"Nope\"},"
                + "\"target\":{\"atom\":\"BoardMarkEventCard\",\"mark\":\"None\"},"
                + "\"action\":{\"atom\":\"SetBoardMark\",\"mark\":\"Missing\",\"random\":true,\"count\":-1,\"excludeSlots\":[0]}"
                + "}");
            var boardMarkValidation = effectSystem.Validate(badBoardMark);
            Assert.IsFalse(boardMarkValidation.IsValid);
            AssertHasIssue(boardMarkValidation, "schema.trigger.mark");
            AssertHasIssue(boardMarkValidation, "schema.target.mark");
            AssertHasIssue(boardMarkValidation, "schema.action.mark");
            AssertHasIssue(boardMarkValidation, "schema.range.count");
            AssertHasIssue(boardMarkValidation, "schema.range.excludeSlots");
        }

        [Test]
        public void ValueExpressionReadsPlayerTargetAndEventValuesDeterministically()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Attack, 7);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 10);
            var monster = CreateMonster("monster.value_expr", 30, SlotId.Board(2));
            monster.Stats.SetBase(StatId.Attack, 3);

            var damageDefinition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.value.damage\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"SlotCard\",\"slot\":2},"
                + "\"action\":{\"atom\":\"Sequence\",\"actions\":["
                + "{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Player\",\"stat\":\"Attack\"},\"actor\":\"Player\"},"
                + "{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Target\",\"stat\":\"Attack\"},\"actor\":\"Player\"}"
                + "]}"
                + "}");
            Assert.IsTrue(Effects().Validate(damageDefinition).IsValid);
            Effects().Activate(damageDefinition, new EffectOwner(EffectContainerType.HelpCard, "test.value.damage", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(999));

            Assert.AreEqual(20, monster.Stats.GetBase(StatId.Hp));

            var eventDefinition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.value.event\","
                + "\"typeTag\":\"【类型玩家技能】\","
                + "\"containerType\":\"PlayerSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnDamageTaken\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"GainArmor\",\"value\":{\"source\":\"Event\",\"field\":\"Amount\"}}"
                + "}");
            Assert.IsTrue(Effects().Validate(eventDefinition).IsValid);
            Effects().Activate(eventDefinition, new EffectOwner(EffectContainerType.PlayerSkill, "test.value.event", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(monster.Uid, avatar.Uid, 6));

            Assert.AreEqual(6, avatar.Stats.GetBase(StatId.Armor));
        }

        [Test]
        public void OnBattleTriggerCanFilterDamageSourceAndRejectRecursiveEffectDamage()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            var monster = CreateMonster("monster.thorn.target", 20, SlotId.Board(2));
            monster.Stats.SetBase(StatId.Attack, 4);

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.thorn.guard\","
                + "\"typeTag\":\"【类型玩家技能】\","
                + "\"containerType\":\"PlayerSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0},"
                + "\"target\":{\"atom\":\"EventTarget\"},"
                + "\"action\":{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Target\",\"stat\":\"Attack\"},\"actor\":\"Player\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.PlayerSkill, "test.thorn.guard", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, monster.Uid, 1));

            Assert.AreEqual(15, monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(1, CountEvents(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.EffectTriggered));

            var invalidDepth = Effects().ParseJson(
                "{"
                + "\"id\":\"test.bad.depth\","
                + "\"typeTag\":\"【类型玩家技能】\","
                + "\"containerType\":\"PlayerSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnBattle\",\"targetKind\":\"Bogus\",\"maxActionDepth\":-1},"
                + "\"target\":{\"atom\":\"EventTarget\"},"
                + "\"action\":{\"atom\":\"DealDamage\",\"amount\":1}"
                + "}");
            var validation = Effects().Validate(invalidDepth);
            Assert.IsFalse(validation.IsValid);
            AssertHasIssue(validation, "schema.trigger.targetKind");
            AssertHasIssue(validation, "schema.range.maxActionDepth");
        }

        [Test]
        public void RepresentativeCatalogDslPassesValidationAndGoldenSnapshots()
        {
            var effectSystem = Effects();
            var effectIds = new[]
            {
                "skill.recombine_head.move",
                "skill.falling_rocks.cumulative",
                "relic.phoenix_feather.fatal",
                "relic.junk_slot_machine.use",
                "skill.stray_cub.slot6",
                "relic.dragon_scale_armor.rule",
                "relic.craving.rule",
                "relic.wood_sword.set",
                "relic.junk_launcher.volley",
                "help.fireball.use",
                "help.food_card.use",
                "help.impact_tutorial.use",
                "help.shield_bash_tutorial.use",
                "help.rotation_wheel.use",
                "help.ward_magic_card.use",
                "skill.stray_cub.first_strike",
                "skill.first_strike.rule",
                "skill.blessing.rule",
                "skill.thief_claims.move",
                "skill.unstable.move",
                "skill.random_walk.move",
                "help.common_chest_card.use",
                "help.blue_chest_card.use",
                "help.golden_chest_card.use",
                "help.blood_conversion.use",
                "skill.easy_road.node_end",
                "relic.lucky_coin.elite_kill",
                "relic.lucky_coin.boss_kill",
                "skill.thorn_skin.battle",
                "skill.learning_growth.gain",
                "skill.intense_burning.flame_deal",
                "skill.violence_maniac.move",
                "skill.violence_nutrition.monster_remove",
                "skill.violence_nutrition.help_remove"
            };

            for (var i = 0; i < effectIds.Length; i++)
            {
                var definition = effectSystem.ParseJson(P5CatalogTestSupport.RequireEffectJson(effectIds[i]));
                var validation = effectSystem.Validate(definition);
                Assert.IsTrue(validation.IsValid, effectIds[i] + " => " + FirstIssue(validation));
                Assert.IsFalse(string.IsNullOrEmpty(validation.GoldenSnapshot));
            }
        }

        [Test]
        public void RecombineHeadCatalogDslRemovesAdjacentSkullAndShufflesBigSkeleton()
        {
            var architecture = NineGridArchitecture.Current;
            var deck = architecture.GetModel<DeckModel>();
            var headless = CreateMonster("monster.headless_skeleton", 6, SlotId.Board(4));
            CreateMonster("monster.skull_head", 3, SlotId.Board(2));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.recombine_head.move",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.recombine_head", headless.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(headless.Uid, SlotId.Board(1)));

            Assert.AreEqual(0, CountBoardMonsters(architecture));
            Assert.AreEqual(1, deck.DrawPileUids.Count);
            Assert.AreEqual("monster.big_skeleton", architecture.GetModel<CardRegistry>().Get(deck.DrawPileUids[0]).DefId);
        }

        [Test]
        public void CumulativeArmorLostCatalogDslShufflesStoneCard()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Armor, 10);
            var megalith = CreateMonster("monster.megalith", 8, SlotId.Board(1));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.falling_rocks.cumulative",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.falling_rocks", megalith.Uid));

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new DealDamageAction(0, avatar.Uid, 7));
            Assert.AreEqual(0, architecture.GetModel<DeckModel>().DrawPileUids.Count);

            pipeline.Execute(new DealDamageAction(0, avatar.Uid, 3));
            var deck = architecture.GetModel<DeckModel>();
            Assert.AreEqual(1, deck.DrawPileUids.Count);
            Assert.AreEqual("monster.stone_man", architecture.GetModel<CardRegistry>().Get(deck.DrawPileUids[0]).DefId);
        }

        [Test]
        public void FatalDamagePhoenixCatalogDslHealsAndRemovesItsOwnRelic()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();
            var avatar = Avatar();
            player.AddRelic("relic.phoenix_feather");

            var instance = P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "relic.phoenix_feather.fatal",
                new EffectOwner(EffectContainerType.Relic, "relic.phoenix_feather", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(0, avatar.Uid, 99));

            Assert.AreEqual(15, avatar.Stats.GetBase(StatId.Hp));
            Assert.IsFalse(Contains(player.RelicDefIds, "relic.phoenix_feather"));
            EffectInstance ignored;
            Assert.IsFalse(Effects().TryGetInstance(instance.InstanceId, out ignored));
        }

        [Test]
        public void WeightedRandomSlotMachineCatalogDslUsesDeterministicChoice()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "relic.junk_slot_machine.use",
                new EffectOwner(EffectContainerType.Relic, "relic.junk_slot_machine", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(123));

            Assert.AreEqual(9, player.Coins.Value);
            Assert.IsTrue(architecture.GetSystem<IActionPipelineSystem>().EventLog.Contains(CoreEventType.EffectTriggered));
        }

        [Test]
        public void ConditionalAuraCatalogDslTurnsOffWhenOwnerLeavesSlot()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var cub = CreateMonster("monster.wandering_child", 1, SlotId.Board(6));
            cub.Stats.SetBase(StatId.Attack, 1);

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.stray_cub.slot6",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.stray_cub", cub.Uid));

            Assert.AreEqual(3, statSystem.GetEffectiveInt(cub, StatId.Attack));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(cub.Uid, SlotId.Board(4)));

            Assert.AreEqual(1, statSystem.GetEffectiveInt(cub, StatId.Attack));
        }

        [Test]
        public void DragonScaleArmorCatalogDslRegistersEnemyAttackRuleModifier()
        {
            P5CatalogTestSupport.ActivateCatalogEffect(
                NineGridArchitecture.Current,
                "relic.dragon_scale_armor.rule",
                new EffectOwner(EffectContainerType.Relic, "relic.dragon_scale_armor", 0));

            Assert.AreEqual(-1f, NineGridArchitecture.Current.GetSystem<IStatSystem>().EvaluateRule(RuleId.EnemyAttackDelta, 0f));
        }

        [Test]
        public void CravingCatalogDslDoublesHealingThroughRuleModifier()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Hp, 10);

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "relic.craving.rule",
                new EffectOwner(EffectContainerType.Relic, "relic.craving", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new HealAction(avatar.Uid, avatar.Uid, 5));

            Assert.AreEqual(20, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void AddRuleModifierAtomPreventsOnlyTheNextMatchingDamage()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            var monster = CreateMonster("monster.rule.damage", 20, SlotId.Board(2));
            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.rule.prevent.next\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"AddRuleModifier\",\"rule\":\"DamageMultiplier\",\"op\":\"Override\",\"value\":0,\"layer\":\"Temporary\",\"scope\":\"Once\",\"source\":\"test.prevent\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.HelpCard, "test.rule.prevent.next", 0));

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new UseItemAction(6001));
            pipeline.Execute(new DealDamageAction(monster.Uid, avatar.Uid, 7));
            Assert.AreEqual(30, avatar.Stats.GetBase(StatId.Hp));

            pipeline.Execute(new DealDamageAction(monster.Uid, avatar.Uid, 7));
            Assert.AreEqual(23, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void TargetedRuleModifierPreventsDamageOnlyForItsOwner()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            var protectedMonster = CreateMonster("monster.blessed", 20, SlotId.Board(2));
            var otherMonster = CreateMonster("monster.other", 20, SlotId.Board(4));
            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.rule.targeted\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"RuleModifier\","
                + "\"ruleModifier\":{\"rule\":\"DamageMultiplier\",\"target\":\"Self\",\"op\":\"Override\",\"value\":0,\"layer\":\"Temporary\",\"scope\":\"Once\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            var instance = Effects().Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.rule.targeted", protectedMonster.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, otherMonster.Uid, 5));
            Assert.AreEqual(15, otherMonster.Stats.GetBase(StatId.Hp));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, protectedMonster.Uid, 5));
            Assert.AreEqual(20, protectedMonster.Stats.GetBase(StatId.Hp));
            EffectInstance ignored;
            Assert.IsFalse(Effects().TryGetInstance(instance.InstanceId, out ignored));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(avatar.Uid, protectedMonster.Uid, 5));
            Assert.AreEqual(15, protectedMonster.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void WoodSetCatalogDslControlsModifierAtEvaluationTime()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();
            var avatar = Avatar();
            P5CatalogTestSupport.ActivateWoodSet(architecture);

            Assert.AreEqual(4, architecture.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Attack));

            player.RemoveRelic("relic.wood_armor");

            Assert.AreEqual(2, architecture.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Attack));
        }

        [Test]
        public void RandomMonsterTargetAndRepeatCatalogDslDealDamageTwice()
        {
            var architecture = NineGridArchitecture.Current;
            var first = CreateMonster("monster.first", 10, SlotId.Board(1));
            var second = CreateMonster("monster.second", 10, SlotId.Board(3));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "relic.junk_launcher.volley",
                new EffectOwner(EffectContainerType.Relic, "relic.junk_launcher", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(100));

            var totalHp = first.Stats.GetBase(StatId.Hp) + second.Stats.GetBase(StatId.Hp);
            Assert.AreEqual(16, totalHp);
        }

        [Test]
        public void GrantRewardFromPoolActionRollsAndGrantsContent()
        {
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IRngUtility>().SetSeed(3UL);

            architecture.GetSystem<IActionPipelineSystem>().Execute(new GrantRewardFromPoolAction("relic.blood_conversion"));

            Assert.IsTrue(HasRelic(architecture.GetModel<PlayerModel>(), "relic.wood_shield"));
            Assert.IsTrue(architecture.GetSystem<IActionPipelineSystem>().EventLog.Contains(CoreEventType.RelicGranted));
        }

        [Test]
        public void FilteredCardsTargetUsesSeededRandomCandidateForSwap()
        {
            var architecture = NineGridArchitecture.Current;
            var rng = architecture.GetUtility<IRngUtility>();
            rng.SetSeed(20260618UL);
            var owner = CreateMonster("monster.unstable.test", 10, SlotId.Board(1));
            var first = CreateMonster("monster.first.candidate", 10, SlotId.Board(2));
            var second = CreateMonster("monster.second.candidate", 10, SlotId.Board(3));
            var expectedIndex = new DeterministicRngUtility(20260618UL).Range(0, 2);
            var expected = expectedIndex == 0 ? first : second;
            var expectedSlot = expected.Slot.Value;

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.filtered.swap\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
                + "\"target\":{\"atom\":\"FilteredCards\",\"include\":[\"Self\"],\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"],\"random\":true,\"count\":1},"
                + "\"action\":{\"atom\":\"Swap\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.filtered.swap", owner.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(owner.Uid, SlotId.Board(5)));

            Assert.AreEqual(expectedSlot, owner.Slot.Value);
            Assert.AreEqual(SlotId.Board(5), expected.Slot.Value);
        }

        [Test]
        public void FilteredCardsTargetNoCandidateDoesNotEmitSideEffects()
        {
            var architecture = NineGridArchitecture.Current;
            var owner = CreateMonster("monster.thief.test", 10, SlotId.Board(1));
            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.filtered.noop\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
                + "\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"HelpCard\",\"zone\":\"Board\",\"adjacentTo\":\"Self\"},"
                + "\"action\":{\"atom\":\"RemoveCard\",\"destination\":\"Removed\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.filtered.noop", owner.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(owner.Uid, SlotId.Board(2)));

            Assert.IsFalse(architecture.GetSystem<IActionPipelineSystem>().EventLog.Contains(CoreEventType.CardRemoved));
        }

        [Test]
        public void SelectedCardsTargetUsesUseItemPayloadAndRejectsAvatar()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var first = CreateMonster("monster.selected.first", 10, SlotId.Board(1));
            var second = CreateMonster("monster.selected.second", 10, SlotId.Board(2));
            var firstSlot = first.Slot.Value;
            var secondSlot = second.Slot.Value;

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.selected.swap\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"SelectedCards\",\"zone\":\"Board\",\"count\":2},"
                + "\"action\":{\"atom\":\"Swap\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.HelpCard, "help.selected.test", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(9001, new[] { avatar.Uid, first.Uid }));
            Assert.AreEqual(firstSlot, first.Slot.Value);
            Assert.AreEqual(secondSlot, second.Slot.Value);

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(9001, new[] { first.Uid, second.Uid }));
            Assert.AreEqual(secondSlot, first.Slot.Value);
            Assert.AreEqual(firstSlot, second.Slot.Value);
        }

        [Test]
        public void SelectedOptionConditionChoosesConfiguredBranch()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = architecture.GetModel<CardRegistry>().Get(architecture.GetModel<BoardModel>().AvatarUid.Value);

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.selected.option\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Armor\"},"
                + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":3}}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.HelpCard, "help.selected.option", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(9001, null, "Attack"));
            Assert.AreEqual(0, avatar.Stats.GetBase(StatId.Armor));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(9001, null, "Armor"));
            Assert.AreEqual(3, avatar.Stats.GetBase(StatId.Armor));
        }

        [Test]
        public void MoveToDrawPileAtomShufflesExistingSelectedCard()
        {
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IRngUtility>().SetSeed(20260618UL);
            var deck = architecture.GetModel<DeckModel>();
            var target = CreateMonster("monster.teleport.target", 10, SlotId.Board(1));

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.selected.teleport\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"SelectedCards\",\"zone\":\"Board\",\"count\":1},"
                + "\"action\":{\"atom\":\"MoveToDrawPile\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.HelpCard, "help.selected.teleport", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(9001, new[] { target.Uid }));

            Assert.AreEqual(ZoneId.DrawPile, target.Zone.Value);
            Assert.AreEqual(SlotId.None, target.Slot.Value);
            Assert.IsTrue(ContainsUid(deck.DrawPileUids, target.Uid));
            Assert.IsTrue(architecture.GetSystem<IActionPipelineSystem>().EventLog.Contains(CoreEventType.CardDealt));
        }

        [Test]
        public void RotateAtomSupportsCounterClockwiseMovement()
        {
            var architecture = NineGridArchitecture.Current;
            var monster = CreateMonster("monster.rotate.target", 10, SlotId.Board(1));
            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.rotate.counter\","
                + "\"typeTag\":\"【类型帮助卡】\","
                + "\"containerType\":\"HelpCard\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Rotate\",\"direction\":\"CounterClockwise\"}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid);
            Effects().Activate(definition, new EffectOwner(EffectContainerType.HelpCard, "test.rotate.counter", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(501));

            Assert.AreEqual(SlotId.Board(4), monster.Slot.Value);
            Assert.IsTrue(HasBoardRotatedEvent(architecture.GetSystem<IActionPipelineSystem>().EventLog, -1, "counterClockwise"));
        }

        [Test]
        public void LearningGrowthCatalogDslRespondsToOtherMonsterAttackGainWithoutSelfRecursion()
        {
            var architecture = NineGridArchitecture.Current;
            var learner = CreateMonster("monster.learning.growth", 20, SlotId.Board(1));
            var other = CreateMonster("monster.other.buff", 20, SlotId.Board(2));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.learning_growth.gain",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.learning_growth", learner.Uid));

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new ModifyBaseStatAction(other.Uid, StatId.Attack, 1, "test.other.buff", "skill.test_buff"));

            Assert.AreEqual(1, learner.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(1, CountEvents(pipeline.EventLog, CoreEventType.EffectTriggered));

            pipeline.Execute(new ModifyBaseStatAction(learner.Uid, StatId.Attack, 1, "test.self.buff", "skill.test_buff"));
            pipeline.Execute(new ModifyBaseStatAction(other.Uid, StatId.Attack, 1, "skill.learning_growth", "skill.learning_growth"));

            Assert.AreEqual(2, learner.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(1, CountEvents(pipeline.EventLog, CoreEventType.EffectTriggered));
        }

        [Test]
        public void IntenseBurningCatalogDslDuplicatesFlameDealsWithoutRecursiveLoop()
        {
            var architecture = NineGridArchitecture.Current;
            var owner = CreateMonster("monster.intense.burning", 20, SlotId.Board(1));
            var deck = architecture.GetModel<DeckModel>();
            var registry = architecture.GetModel<CardRegistry>();

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.intense_burning.flame_deal",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.intense_burning", owner.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new ShuffleIntoDrawPileAction("help.flame", CardKind.HelpCard, 1, false, "skill.devotion"));

            Assert.AreEqual(2, CountCardsByDef(deck.DrawPileUids, registry, "help.flame"));
            Assert.AreEqual(1, CountEvents(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.EffectTriggered));
        }

        [Test]
        public void ViolenceManiacCatalogDslTagsRemovalsForViolenceNutrition()
        {
            var architecture = NineGridArchitecture.Current;
            var owner = CreateMonster("monster.violence.owner", 20, SlotId.Board(5));
            var monster = CreateMonster("monster.violence.target", 20, SlotId.Board(2));
            var help = architecture.GetModel<CardRegistry>().Create("help.violence.target", CardKind.HelpCard);
            architecture.GetModel<BoardModel>().PlaceCard(help, SlotId.Board(4));

            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.violence_maniac.move",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.violence_maniac", owner.Uid));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.violence_nutrition.monster_remove",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.violence_nutrition", owner.Uid));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.violence_nutrition.help_remove",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.violence_nutrition", owner.Uid));

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new MoveCardAction(owner.Uid, SlotId.Board(8)));
            pipeline.Execute(new MoveCardAction(owner.Uid, SlotId.Board(5)));

            Assert.AreEqual(ZoneId.Removed, monster.Zone.Value);
            Assert.AreEqual(ZoneId.Removed, help.Zone.Value);
            Assert.AreEqual(3, owner.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(5, owner.Stats.GetBase(StatId.Armor));
        }

        [Test]
        public void SetBoardMarkAtomCreatesVisibleRandomBlessedSlot()
        {
            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IRngUtility>().SetSeed(20260618UL);
            var owner = CreateMonster("monster.guide.owner", 20, SlotId.Board(1));
            var expected = ExpectedBlessedSlot(20260618UL);

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.board.mark.create\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
                + "\"target\":{\"atom\":\"Self\"},"
                + "\"action\":{\"atom\":\"SetBoardMark\",\"mark\":\"Blessed\",\"random\":true,\"count\":1,\"onlyUnmarked\":true,\"excludeSlots\":[5]}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid, FirstIssue(Effects().Validate(definition)));
            Effects().Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.board.mark.create", owner.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(owner.Uid, SlotId.Board(2)));

            Assert.IsTrue(architecture.GetModel<BoardModel>().IsBlessed(expected));
            Assert.AreEqual(1, architecture.GetModel<BoardModel>().CountMarkedSlots(BoardMarkId.Blessed));
            Assert.AreEqual(1, CountEvents(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.BoardMarked));
        }

        [Test]
        public void OnMoveToBoardMarkRewardsMarkedMonsterTarget()
        {
            var architecture = NineGridArchitecture.Current;
            var owner = CreateMonster("monster.guide.owner", 20, SlotId.Board(1));
            var mover = CreateMonster("monster.guide.target", 20, SlotId.Board(4));
            mover.Stats.SetBase(StatId.Attack, 2);
            mover.Stats.SetBase(StatId.Armor, 3);
            architecture.GetSystem<IActionPipelineSystem>().Execute(
                new SetBoardMarkAction(SlotId.Board(3), BoardMarkId.Blessed, true, "test", "setup"));

            var definition = Effects().ParseJson(
                "{"
                + "\"id\":\"test.board.mark.enter\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnMoveToBoardMark\",\"mark\":\"Blessed\",\"targetKind\":\"Monster\"},"
                + "\"target\":{\"atom\":\"BoardMarkEventCard\",\"mark\":\"Blessed\",\"targetKind\":\"Monster\"},"
                + "\"action\":{\"atom\":\"Sequence\",\"actions\":["
                + "{\"atom\":\"GainArmor\",\"amount\":2},"
                + "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"test.board.mark\"}"
                + "]}"
                + "}");
            Assert.IsTrue(Effects().Validate(definition).IsValid, FirstIssue(Effects().Validate(definition)));
            Effects().Activate(definition, new EffectOwner(EffectContainerType.MonsterSkill, "test.board.mark.enter", owner.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(mover.Uid, SlotId.Board(3)));

            Assert.AreEqual(5, mover.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(3, mover.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(1, CountEvents(architecture.GetSystem<IActionPipelineSystem>().EventLog, CoreEventType.EffectTriggered));
        }

        [Test]
        public void GoldArmorRuleSpendsGoldBeforeArmorAndNeverShieldsHpDamage()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Armor, 2);
            architecture.GetModel<PlayerModel>().AddCoins(50);
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "relic.gold_armor.rule",
                new EffectOwner(EffectContainerType.Relic, "relic.gold_armor", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(0, avatar.Uid, 5));

            Assert.AreEqual(40, architecture.GetModel<PlayerModel>().Coins.Value);
            Assert.AreEqual(2, avatar.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(27, avatar.Stats.GetBase(StatId.Hp));
            Assert.IsTrue(architecture.GetSystem<IActionPipelineSystem>().EventLog.Contains(CoreEventType.GoldModified));
        }

        [Test]
        public void TauntRuleRejectsOtherAdjacentMonsterUntilTaunterLeavesAdjacency()
        {
            var architecture = NineGridArchitecture.Current;
            var phase = architecture.GetSystem<IPhaseSystem>();
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var taunter = CreateMonster("monster.taunter", 20, SlotId.Board(2));
            var other = CreateMonster("monster.other", 20, SlotId.Board(4));
            P5CatalogTestSupport.ActivateCatalogEffect(
                architecture,
                "skill.taunt.rule",
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.taunt", taunter.Uid));
            pipeline.Execute(new ChangePhaseAction(GamePhase.InteractionLoop));

            var rejected = phase.Attack(other.Slot.Value);
            Assert.IsFalse(rejected.Accepted);
            Assert.AreEqual(20, other.Stats.GetBase(StatId.Hp));

            pipeline.Execute(new MoveCardAction(taunter.Uid, SlotId.Board(1)));
            var accepted = phase.Attack(other.Slot.Value);
            Assert.IsTrue(accepted.Accepted);
            Assert.Less(other.Stats.GetBase(StatId.Hp), 20);
        }

        [Test]
        public void DoublingTowerOnBoardReplaysMonsterTargetedHelpCardWithoutRemovingSelf()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var tower = content.CreateDraft("help.doubling_tower").Create(registry);
            var knife = content.CreateDraft("help.throwing_knife").Create(registry);
            var monster = CreateMonster("monster.double.target", 20, SlotId.Board(2));
            board.PlaceCard(tower, SlotId.Board(1));
            content.ApplyContentToCard(tower);
            content.ApplyContentToCard(knife);

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(knife.Uid, new[] { monster.Uid }));

            Assert.AreEqual(8, monster.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(ZoneId.Board, tower.Zone.Value);
            Assert.IsTrue(Contains(tower.EffectIds, "help.doubling_tower.board_monster"));
        }

        [Test]
        public void DoublingTowerInItemSlotsReplaysPlayerHelpCardOnceThenDeactivates()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Hp, 10);

            architecture.GetSystem<IActionPipelineSystem>().Execute(new SpawnCardAction("help.doubling_tower", CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1));
            var tower = FindCardByDef(architecture.GetModel<DeckModel>().ItemSlotUids, registry, "help.doubling_tower");
            var potion = content.CreateDraft("help.healing_potion").Create(registry);
            content.ApplyContentToCard(potion);

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(potion.Uid, new[] { avatar.Uid }));

            Assert.AreEqual(30, avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(ZoneId.Removed, tower.Zone.Value);
            Assert.IsFalse(Contains(tower.EffectIds, "help.doubling_tower.item_player"));

            avatar.Stats.SetBase(StatId.Hp, 10);
            var secondPotion = content.CreateDraft("help.healing_potion").Create(registry);
            content.ApplyContentToCard(secondPotion);
            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(secondPotion.Uid, new[] { avatar.Uid }));
            Assert.AreEqual(20, avatar.Stats.GetBase(StatId.Hp));
        }

        private static IEffectSystem Effects()
        {
            return NineGridArchitecture.Current.GetSystem<IEffectSystem>();
        }

        private static CardInstance Avatar()
        {
            var architecture = NineGridArchitecture.Current;
            return architecture.GetModel<CardRegistry>().Get(architecture.GetModel<BoardModel>().AvatarUid.Value);
        }

        private static CardInstance CreateMonster(string defId, int hp, SlotId slot)
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            board.PlaceCard(monster, slot);
            return monster;
        }

        private static CardInstance FindCardByDef(IReadOnlyList<int> uids, CardRegistry registry, string defId)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                var card = registry.Get(uids[i]);
                if (card.DefId == defId)
                {
                    return card;
                }
            }

            Assert.Fail("Missing card: " + defId);
            return null;
        }

        private static int CountBoardMonsters(IArchitecture architecture)
        {
            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var count = 0;
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && card.Kind == CardKind.Monster)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertHasIssue(EffectValidationResult validation, string code)
        {
            for (var i = 0; i < validation.Issues.Count; i++)
            {
                if (validation.Issues[i].Code == code)
                {
                    return;
                }
            }

            Assert.Fail("Missing validation issue: " + code);
        }

        private static string FirstIssue(EffectValidationResult validation)
        {
            return validation.Issues.Count == 0 ? string.Empty : validation.Issues[0].ToString();
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

        private static bool HasRelic(PlayerModel player, string relicDefId)
        {
            for (var i = 0; i < player.RelicDefIds.Count; i++)
            {
                if (player.RelicDefIds[i] == relicDefId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBoardRotatedEvent(EventLog eventLog, int amount, string message)
        {
            for (var i = 0; i < eventLog.Entries.Count; i++)
            {
                var entry = eventLog.Entries[i];
                if (entry.Type == CoreEventType.BoardRotated && entry.Amount == amount && entry.Message == message)
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

        private static SlotId ExpectedBlessedSlot(ulong seed)
        {
            var candidates = new[] { 1, 2, 3, 4, 6, 7, 8, 9 };
            var index = new DeterministicRngUtility(seed).Range(0, candidates.Length);
            return SlotId.Board(candidates[index]);
        }
    }
}
