using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 重组：相邻无头骷髅 + 骷髅头在同一旋转批次内只合并一次，只洗入一张大骷髅。
    /// </summary>
    public sealed class RecombineDedupRegressionTests
    {
        private static readonly SlotId sHeadlessSlot = SlotId.Board(4);
        private static readonly SlotId sSkullSlot = SlotId.Board(7);
        private static readonly SlotId sSecondHeadlessSlot = SlotId.Board(2);
        private static readonly SlotId sSecondSkullSlot = SlotId.Board(1);

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
        public void AdjacentFragments_RotateOnce_ShufflesOneBigSkeleton()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSkullSlot);

            var before = CountDefInDrawPile("monster.big_skeleton");
            var startIndex = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new RotateBoardClockwiseAction(true, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(before + 1, CountDefInDrawPile("monster.big_skeleton"), "相邻碎片旋转一次应只洗入一张大骷髅");
            var headTriggers = CountEffectTriggeredSince(startIndex, "skill.recombine_head");
            var bodyTriggers = CountEffectTriggeredSince(startIndex, "skill.recombine_body");
            Assert.AreEqual(1, headTriggers + bodyTriggers, "重组头/身在同一对应只触发其一");
        }

        [Test]
        public void TwoAdjacentPairs_RotateOnce_ShufflesTwoBigSkeletons()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSkullSlot);
            SpawnOnBoard("monster.headless_skeleton", sSecondHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSecondSkullSlot);

            var before = CountDefInDrawPile("monster.big_skeleton");

            mPipeline.Enqueue(new RotateBoardClockwiseAction(true, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.AreEqual(before + 2, CountDefInDrawPile("monster.big_skeleton"), "两对相邻碎片应各合并一张大骷髅");
        }

        [Test]
        public void AdjacentFragments_ThreeRotations_ShufflesThreeBigSkeletonsNotSix()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSkullSlot);

            var before = CountDefInDrawPile("monster.big_skeleton");
            for (var rotation = 0; rotation < 3; rotation++)
            {
                SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
                SpawnOnBoard("monster.skull_head", sSkullSlot);
                mPipeline.Enqueue(new RotateBoardClockwiseAction(true, "test", "test"));
                Assert.Greater(mPipeline.RunToCompletion(), 0);
            }

            Assert.AreEqual(before + 3, CountDefInDrawPile("monster.big_skeleton"), "三次旋转各合并一次，不得双重洗入");
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
    }
}
