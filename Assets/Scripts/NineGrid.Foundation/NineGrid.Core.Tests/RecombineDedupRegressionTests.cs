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
    /// 重组：相邻无头骷髅 + 骷髅头在同一旋转批次内只合并一次，只洗入一张重组大骷髅（不持有散架）。
    /// </summary>
    public sealed class RecombineDedupRegressionTests
    {
        private static readonly SlotId sHeadlessSlot = SlotId.Board(4);
        private static readonly SlotId sSkullSlot = SlotId.Board(7);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 19UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AdjacentFragments_RotateOnce_ShufflesOneBigSkeletonReborn()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSkullSlot);

            var before = CountDefInDrawPile("monster.big_skeleton_reborn");
            var startIndex = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new RotateBoardClockwiseAction(true, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(before + 1, CountDefInDrawPile("monster.big_skeleton_reborn"), "相邻碎片旋转一次应只洗入一张重组大骷髅");
            Assert.AreEqual(0, CountDefInDrawPile("monster.big_skeleton"), "重组不得洗入原生大骷髅");
            var headTriggers = CountEffectTriggeredSince(startIndex, "skill.recombine_head");
            var bodyTriggers = CountEffectTriggeredSince(startIndex, "skill.recombine_body");
            Assert.AreEqual(1, headTriggers + bodyTriggers, "重组头/身在同一对应只触发其一");
        }

        [Test]
        public void BigSkeletonReborn_Killed_DoesNotShuffleFragments()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.big_skeleton_reborn", sHeadlessSlot);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, 12);

            var skullBefore = CountDefInDrawPile("monster.skull_head");
            var headlessBefore = CountDefInDrawPile("monster.headless_skeleton");
            var startIndex = mPipeline.EventLog.Entries.Count;

            var board = mArch.GetModel<BoardModel>();
            var targetUid = board.GetCardUid(sHeadlessSlot);
            Assert.Greater(targetUid, 0);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, targetUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(skullBefore, CountDefInDrawPile("monster.skull_head"), "重组大骷髅被击杀不得洗入骷髅头");
            Assert.AreEqual(headlessBefore, CountDefInDrawPile("monster.headless_skeleton"), "重组大骷髅被击杀不得洗入无头骷髅");
            Assert.IsFalse(ContainsEffectTriggeredSince(startIndex, "skill.fall_apart"), "重组大骷髅不持有散架");
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private void SpawnOnBoard(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0, "应成功生成 " + defId);
            Assert.AreEqual(defId, mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().GetCardUid(slot)).DefId);
        }

        private int CountDefInDrawPile(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var count = 0;
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                if (registry.Get(deck.DrawPileUids[i]).DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountEffectTriggeredSince(int startIndex, string sourceDefId)
        {
            var entries = mPipeline.EventLog.Entries;
            var count = 0;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var evt = entries[i];
                if (evt.Type == CoreEventType.EffectTriggered
                    && evt.SourceDefId == sourceDefId)
                {
                    count++;
                }
            }

            return count;
        }

        private bool ContainsEffectTriggeredSince(int startIndex, string sourceDefId)
        {
            return CountEffectTriggeredSince(startIndex, sourceDefId) > 0;
        }
    }
}
