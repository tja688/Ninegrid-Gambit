using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// Permanent 有效攻卡面旁路：Apply 提交、Temporary 不上屏、拓扑/移除后回落。
    /// </summary>
    public sealed class PermanentAttackFaceCommitTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 77UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AddStatModifier_PermanentAttack_EmitsBaseStatModifiedWithEffective()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var uid = Spawn("monster.headless_skeleton", CardKind.Monster, SlotId.Board(5));
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            card.Stats.SetBase(StatId.Attack, 2);

            var start = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new AddStatModifierAction(
                uid,
                StatId.Attack,
                ModifierOp.Add,
                1f,
                ModifierLayer.Conditional,
                ModifierScope.Permanent,
                "test.permanent",
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(3, mStats.GetEffectiveInt(card, StatId.Attack));
            Assert.IsTrue(HasAttackFaceCommit(start, uid, 3));
        }

        [Test]
        public void AddStatModifier_TemporaryAttack_DoesNotEmitFaceCommit()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var uid = Spawn("monster.headless_skeleton", CardKind.Monster, SlotId.Board(5));
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            card.Stats.SetBase(StatId.Attack, 2);

            var start = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new AddStatModifierAction(
                uid,
                StatId.Attack,
                ModifierOp.Add,
                2f,
                ModifierLayer.Temporary,
                ModifierScope.UntilBattleEnds,
                "test.temp",
                "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(4, mStats.GetEffectiveInt(card, StatId.Attack));
            Assert.IsFalse(
                HasAttackFaceCommit(start, uid, 4),
                "Temporary 交战加成不得提交卡面攻");
        }

        [Test]
        public void SwapBoard_ConditionalAuraInactive_EmitsFaceCommitBackToBase()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);

            var hostUid = Spawn("monster.headless_skeleton", CardKind.Monster, SlotId.Board(5));
            var allyUid = Spawn("monster.skull_head", CardKind.Monster, SlotId.Board(2));
            var registry = mArch.GetModel<CardRegistry>();
            var ally = registry.Get(allyUid);
            ally.Stats.SetBase(StatId.Attack, 2);

            mPipeline.Enqueue(new AddStatModifierAction(
                allyUid,
                StatId.Attack,
                ModifierOp.Add,
                1f,
                ModifierLayer.Conditional,
                ModifierScope.Permanent,
                "test.aura",
                "test",
                new SourceAdjacentToUidCondition(hostUid, allyUid),
                replaceSameSource: true));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            Assert.AreEqual(3, mStats.GetEffectiveInt(ally, StatId.Attack));

            var start = mPipeline.EventLog.Entries.Count;
            mPipeline.Enqueue(new SwapBoardSlotsAction(SlotId.Board(2), SlotId.Board(1)));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            ally = registry.Get(allyUid);
            Assert.AreEqual(2, mStats.GetEffectiveInt(ally, StatId.Attack));
            Assert.IsTrue(
                HasAttackFaceCommit(start, allyUid, 2),
                "离开邻接后拓扑旁路应提交有效攻=基值");
        }

        private int Spawn(string defId, CardKind kind, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, kind, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            return mArch.GetModel<BoardModel>().GetCardUid(slot);
        }

        private bool HasAttackFaceCommit(int startIndex, int cardUid, int expectedAttack)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type != CoreEventType.BaseStatModified)
                {
                    continue;
                }

                var uid = e.CardUid > 0 ? e.CardUid : e.TargetUid;
                if (uid == cardUid
                    && (StatId)e.Amount == StatId.Attack
                    && e.ResultValue == expectedAttack)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
