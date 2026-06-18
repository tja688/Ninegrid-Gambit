using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P2StatPipelineTests
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
        public void PersistentModifiersAreIncludedInEffectiveValue()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var card = CreateMonster("monster.bat");
            card.Stats.SetBase(StatId.Attack, 2);

            statSystem.AddModifier(card, new StatModifier(
                StatId.Attack,
                ModifierOp.Add,
                3,
                ModifierLayer.Persistent,
                new ModifierSource("relic.attack_plus"),
                ModifierScope.Permanent));

            Assert.AreEqual(5, statSystem.GetEffectiveInt(card, StatId.Attack));
            Assert.AreEqual(5, architecture.SendQuery(new EffectiveStatQuery(card.Uid, StatId.Attack)));
        }

        [Test]
        public void ConditionalModifierOnlyCountsWhenConditionIsMet()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var board = architecture.GetModel<BoardModel>();
            var card = CreateMonster("monster.guard");
            card.Stats.SetBase(StatId.Armor, 1);
            board.PlaceCard(card, SlotId.Board(1));

            statSystem.AddModifier(card, new StatModifier(
                StatId.Armor,
                ModifierOp.Add,
                4,
                ModifierLayer.Conditional,
                new ModifierSource("slot.two_aura"),
                ModifierScope.Permanent,
                new AtSlotCondition(SlotId.Board(2))));

            Assert.AreEqual(1, statSystem.GetEffectiveInt(card, StatId.Armor));

            board.PlaceCard(card, SlotId.Board(2));
            Assert.AreEqual(5, statSystem.GetEffectiveInt(card, StatId.Armor));
        }

        [Test]
        public void HpBelowAndAdjacentConditionsAreSupported()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var board = architecture.GetModel<BoardModel>();
            var card = CreateMonster("monster.wolf");
            card.Stats.SetBase(StatId.MaxHp, 10);
            card.Stats.SetBase(StatId.Hp, 4);
            card.Stats.SetBase(StatId.Attack, 1);
            board.PlaceCard(card, SlotId.Board(4));

            statSystem.AddModifier(card, new StatModifier(
                StatId.Attack,
                ModifierOp.Add,
                2,
                ModifierLayer.Conditional,
                new ModifierSource("low_hp"),
                ModifierScope.Permanent,
                new HpBelowPctCondition(0.5f)));
            statSystem.AddModifier(card, new StatModifier(
                StatId.Attack,
                ModifierOp.Add,
                3,
                ModifierLayer.Conditional,
                new ModifierSource("adjacent_center"),
                ModifierScope.Permanent,
                new AdjacentCondition(SlotId.Board(5))));

            Assert.AreEqual(6, statSystem.GetEffectiveInt(card, StatId.Attack));
        }

        [Test]
        public void TemporaryScopesCanBeClearedIndependently()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var card = CreateMonster("monster.temporary");
            card.Stats.SetBase(StatId.Attack, 1);

            statSystem.AddModifier(card, new StatModifier(StatId.Attack, ModifierOp.Add, 10, ModifierLayer.Temporary, new ModifierSource("once"), ModifierScope.Once));
            statSystem.AddModifier(card, new StatModifier(StatId.Attack, ModifierOp.Add, 20, ModifierLayer.Temporary, new ModifierSource("enemy"), ModifierScope.UntilEnemyChanges));
            statSystem.AddModifier(card, new StatModifier(StatId.Attack, ModifierOp.Add, 30, ModifierLayer.Temporary, new ModifierSource("battle"), ModifierScope.UntilBattleEnds));

            Assert.AreEqual(61, statSystem.GetEffectiveInt(card, StatId.Attack));
            Assert.AreEqual(1, statSystem.ClearModifiersByScope(card, ModifierScope.Once));
            Assert.AreEqual(51, statSystem.GetEffectiveInt(card, StatId.Attack));
            Assert.AreEqual(1, statSystem.ClearModifiersByScope(card, ModifierScope.UntilEnemyChanges));
            Assert.AreEqual(31, statSystem.GetEffectiveInt(card, StatId.Attack));
            Assert.AreEqual(1, statSystem.ClearModifiersByScope(card, ModifierScope.UntilBattleEnds));
            Assert.AreEqual(1, statSystem.GetEffectiveInt(card, StatId.Attack));
        }

        [Test]
        public void RemovingSourceOnlyClearsThatSource()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var card = CreateMonster("monster.sources");
            var sourceA = new ModifierSource("source.a");
            var sourceB = new ModifierSource("source.b");
            card.Stats.SetBase(StatId.Attack, 1);

            statSystem.AddModifier(card, new StatModifier(StatId.Attack, ModifierOp.Add, 2, ModifierLayer.Persistent, sourceA, ModifierScope.Permanent));
            statSystem.AddModifier(card, new StatModifier(StatId.Attack, ModifierOp.Add, 5, ModifierLayer.Persistent, sourceB, ModifierScope.Permanent));

            Assert.AreEqual(8, statSystem.GetEffectiveInt(card, StatId.Attack));
            Assert.AreEqual(1, statSystem.RemoveModifiersBySource(card, sourceA));
            Assert.AreEqual(6, statSystem.GetEffectiveInt(card, StatId.Attack));
        }

        [Test]
        public void RuleModifierRegistryEvaluatesAndRemovesBySource()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var source = new ModifierSource("relic.craving");

            statSystem.RuleModifiers.Add(new RuleModifier(
                RuleId.RecoveryMultiplier,
                ModifierOp.Multiply,
                2,
                ModifierLayer.Persistent,
                source,
                ModifierScope.Permanent));

            Assert.AreEqual(6f, statSystem.EvaluateRule(RuleId.RecoveryMultiplier, 3f));
            Assert.AreEqual(1, statSystem.RuleModifiers.RemoveBySource(source));
            Assert.AreEqual(3f, statSystem.EvaluateRule(RuleId.RecoveryMultiplier, 3f));
        }

        private static CardInstance CreateMonster(string defId)
        {
            return NineGridArchitecture.Current.GetModel<CardRegistry>().Create(defId, CardKind.Monster);
        }
    }
}
