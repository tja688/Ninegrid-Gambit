using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
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
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("OwnsRelicSet"));
            Assert.IsTrue(effectSystem.AtomRegistry.Conditions.ContainsKey("AdjacentHasCard"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("RandomMonster"));
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("AdjacentCard"));
            Assert.IsTrue(effectSystem.AtomRegistry.Actions.ContainsKey("WeightedRandom"));

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
                "help.shield_bash_tutorial.use"
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
    }
}
