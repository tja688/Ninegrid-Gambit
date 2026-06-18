using System.Collections.Generic;
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
            Assert.IsTrue(effectSystem.AtomRegistry.Targets.ContainsKey("RandomMonster"));
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
        public void SelfMoveAdjacentSequenceCanRunThreeActions()
        {
            var architecture = NineGridArchitecture.Current;
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var player = architecture.GetModel<PlayerModel>();
            var avatar = Avatar();
            var head = CreateMonster("monster.recombine_head", 5, SlotId.Board(1));

            Activate(
                RecombineHeadJson(),
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.recombine_head", head.Uid));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(head.Uid, SlotId.Board(2)));

            Assert.AreEqual(1, avatar.Stats.GetBase(StatId.Armor));
            Assert.AreEqual(2, player.Coins.Value);
            Assert.AreEqual(1, deck.DrawPileUids.Count);
            Assert.AreEqual("monster.big_skull", architecture.GetModel<CardRegistry>().Get(deck.DrawPileUids[0]).DefId);
            Assert.AreEqual(SlotId.Board(2), board.GetCardUid(SlotId.Board(2)) == head.Uid ? head.Slot.Value : SlotId.None);
        }

        [Test]
        public void CumulativeArmorLostThresholdShufflesStoneCard()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Armor, 10);

            Activate(
                CumulativeArmorLostJson(),
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.falling_rocks", avatar.Uid));

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(new DealDamageAction(0, avatar.Uid, 7));
            Assert.AreEqual(0, architecture.GetModel<DeckModel>().DrawPileUids.Count);

            pipeline.Execute(new DealDamageAction(0, avatar.Uid, 3));
            var deck = architecture.GetModel<DeckModel>();
            Assert.AreEqual(1, deck.DrawPileUids.Count);
            Assert.AreEqual("monster.stone_legion", architecture.GetModel<CardRegistry>().Get(deck.DrawPileUids[0]).DefId);
        }

        [Test]
        public void FatalDamagePhoenixHealsAndRemovesItsOwnRelic()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();
            var avatar = Avatar();
            player.AddRelic("relic.phoenix_feather");

            var instance = Activate(
                PhoenixFeatherJson(),
                new EffectOwner(EffectContainerType.Relic, "relic.phoenix_feather", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new DealDamageAction(0, avatar.Uid, 99));

            Assert.AreEqual(15, avatar.Stats.GetBase(StatId.Hp));
            Assert.IsFalse(Contains(player.RelicDefIds, "relic.phoenix_feather"));
            EffectInstance ignored;
            Assert.IsFalse(Effects().TryGetInstance(instance.InstanceId, out ignored));
        }

        [Test]
        public void WeightedRandomSlotMachineUsesDeterministicChoice()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();

            Activate(
                SlotMachineJson(),
                new EffectOwner(EffectContainerType.Relic, "relic.junk_slot_machine", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(123));

            Assert.AreEqual(9, player.Coins.Value);
            Assert.IsTrue(architecture.GetSystem<IActionPipelineSystem>().EventLog.Contains(CoreEventType.EffectTriggered));
        }

        [Test]
        public void ConditionalAuraModifierTurnsOffWhenOwnerLeavesSlot()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var cub = CreateMonster("monster.stray_cub", 10, SlotId.Board(6));
            cub.Stats.SetBase(StatId.Attack, 1);

            Activate(
                StrayCubAuraJson(),
                new EffectOwner(EffectContainerType.MonsterSkill, "skill.stray_cub", cub.Uid));

            Assert.AreEqual(3, statSystem.GetEffectiveInt(cub, StatId.Attack));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new MoveCardAction(cub.Uid, SlotId.Board(4)));

            Assert.AreEqual(1, statSystem.GetEffectiveInt(cub, StatId.Attack));
        }

        [Test]
        public void DragonScaleArmorRegistersEnemyAttackRuleModifier()
        {
            var statSystem = NineGridArchitecture.Current.GetSystem<IStatSystem>();

            Activate(
                DragonScaleArmorJson(),
                new EffectOwner(EffectContainerType.Relic, "relic.dragon_scale_armor", 0));

            Assert.AreEqual(-1f, statSystem.EvaluateRule(RuleId.EnemyAttackDelta, 0f));
        }

        [Test]
        public void CravingDoublesHealingThroughRuleModifier()
        {
            var architecture = NineGridArchitecture.Current;
            var avatar = Avatar();
            avatar.Stats.SetBase(StatId.Hp, 10);

            Activate(
                CravingJson(),
                new EffectOwner(EffectContainerType.Relic, "relic.craving", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new HealAction(avatar.Uid, avatar.Uid, 5));

            Assert.AreEqual(20, avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void RelicSetConditionControlsModifierAtEvaluationTime()
        {
            var architecture = NineGridArchitecture.Current;
            var player = architecture.GetModel<PlayerModel>();
            var avatar = Avatar();
            player.AddRelic("relic.wood_shield");
            player.AddRelic("relic.wood_sword");
            player.AddRelic("relic.wood_armor");

            Activate(
                WoodSetBonusJson(),
                new EffectOwner(EffectContainerType.Relic, "relic.wood_sword", 0));

            Assert.AreEqual(3, architecture.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Attack));

            player.RemoveRelic("relic.wood_armor");

            Assert.AreEqual(1, architecture.GetSystem<IStatSystem>().GetEffectiveInt(avatar, StatId.Attack));
        }

        [Test]
        public void RandomMonsterTargetAndRepeatCompositeDealDamageTwice()
        {
            var architecture = NineGridArchitecture.Current;
            var first = CreateMonster("monster.first", 10, SlotId.Board(1));
            var second = CreateMonster("monster.second", 10, SlotId.Board(3));

            Activate(
                RandomRepeatDamageJson(),
                new EffectOwner(EffectContainerType.Relic, "relic.random_bolts", 0));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(100));

            var totalHp = first.Stats.GetBase(StatId.Hp) + second.Stats.GetBase(StatId.Hp);
            Assert.AreEqual(16, totalHp);
        }

        private static IEffectSystem Effects()
        {
            return NineGridArchitecture.Current.GetSystem<IEffectSystem>();
        }

        private static EffectInstance Activate(string json, EffectOwner owner)
        {
            var effectSystem = Effects();
            return effectSystem.Activate(effectSystem.ParseJson(json), owner);
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

        private static string RecombineHeadJson()
        {
            return "{"
                + "\"id\":\"skill.recombine_head\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnSelfMove\",\"every\":1},"
                + "\"conditions\":[{\"atom\":\"Adjacent\",\"left\":\"Self\",\"slot\":5}],"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Sequence\",\"actions\":["
                + "{\"atom\":\"GainArmor\",\"amount\":1},"
                + "{\"atom\":\"ModifyGold\",\"delta\":2,\"reason\":\"recombine\"},"
                + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.big_skull\",\"kind\":\"Monster\",\"count\":1,\"top\":true}"
                + "]}"
                + "}";
        }

        private static string CumulativeArmorLostJson()
        {
            return "{"
                + "\"id\":\"skill.falling_rocks\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnCumulative\",\"metric\":\"armorLost\",\"threshold\":10},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"ShuffleInto\",\"defId\":\"monster.stone_legion\",\"kind\":\"Monster\",\"count\":1,\"top\":true}"
                + "}";
        }

        private static string PhoenixFeatherJson()
        {
            return "{"
                + "\"id\":\"relic.phoenix_feather\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnFatalDamage\",\"target\":\"Player\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Sequence\",\"actions\":["
                + "{\"atom\":\"Heal\",\"amount\":15,\"actor\":\"Player\"},"
                + "{\"atom\":\"DeactivateSelfEffect\"}"
                + "]}"
                + "}";
        }

        private static string SlotMachineJson()
        {
            return "{"
                + "\"id\":\"relic.junk_slot_machine\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"WeightedRandom\",\"choices\":["
                + "{\"weight\":1,\"action\":{\"atom\":\"ModifyGold\",\"delta\":9,\"reason\":\"slot\"}},"
                + "{\"weight\":0,\"action\":{\"atom\":\"GainArmor\",\"amount\":99}}"
                + "]}"
                + "}";
        }

        private static string StrayCubAuraJson()
        {
            return "{"
                + "\"id\":\"skill.stray_cub_slot6\","
                + "\"typeTag\":\"【类型怪物技能】\","
                + "\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Modifier\","
                + "\"target\":{\"atom\":\"Self\"},"
                + "\"conditions\":[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":6}],"
                + "\"modifier\":{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"
                + "}";
        }

        private static string DragonScaleArmorJson()
        {
            return "{"
                + "\"id\":\"relic.dragon_scale_armor\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"RuleModifier\","
                + "\"ruleModifier\":{\"rule\":\"EnemyAttackDelta\",\"op\":\"Add\",\"value\":-1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"
                + "}";
        }

        private static string CravingJson()
        {
            return "{"
                + "\"id\":\"relic.craving\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"RuleModifier\","
                + "\"ruleModifier\":{\"rule\":\"RecoveryMultiplier\",\"op\":\"Multiply\",\"value\":2,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"
                + "}";
        }

        private static string WoodSetBonusJson()
        {
            return "{"
                + "\"id\":\"relic.wood_set_bonus\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Modifier\","
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"conditions\":[{\"atom\":\"OwnsRelicSet\",\"defIds\":[\"relic.wood_shield\",\"relic.wood_sword\",\"relic.wood_armor\"]}],"
                + "\"modifier\":{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"
                + "}";
        }

        private static string RandomRepeatDamageJson()
        {
            return "{"
                + "\"id\":\"relic.random_bolts\","
                + "\"typeTag\":\"【类型遗物】\","
                + "\"containerType\":\"Relic\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnUseHelpCard\"},"
                + "\"target\":{\"atom\":\"RandomMonster\"},"
                + "\"action\":{\"atom\":\"Repeat\",\"count\":2,\"action\":{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Player\"}}"
                + "}";
        }
    }
}
