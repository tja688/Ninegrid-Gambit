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
    /// 散架 OnRemove：仅本卡被移除时洗入碎片；场上其他大骷髅不得因他卡移除误触发。
    /// 对照骷髅军团 drawPile 膨胀（recombine / 击杀碎片时误触发 fall_apart）。
    /// </summary>
    public sealed class FallApartOnRemoveRegressionTests
    {
        private static readonly SlotId sBigSkeletonSlot = SlotId.Board(2);
        private static readonly SlotId sVictimAdjacentSlot = SlotId.Board(4);
        private static readonly SlotId sSecondBigSkeletonSlot = SlotId.Board(9);
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
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BigSkeleton_OnBoard_OtherMonsterKilled_DoesNotShuffleFragments()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.big_skeleton", sBigSkeletonSlot);
            SpawnOnBoard("monster.headless_skeleton", sVictimAdjacentSlot);
            PrepareAvatarAttack(10);

            var skullBefore = CountDefInDrawPile("monster.skull_head");
            var headlessBefore = CountDefInDrawPile("monster.headless_skeleton");
            var startIndex = mPipeline.EventLog.Entries.Count;

            var board = mArch.GetModel<BoardModel>();
            var victimUid = board.GetCardUid(sVictimAdjacentSlot);
            Assert.Greater(victimUid, 0);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(skullBefore, CountDefInDrawPile("monster.skull_head"), "击杀无头骷髅不得洗入骷髅头");
            Assert.AreEqual(headlessBefore, CountDefInDrawPile("monster.headless_skeleton"), "击杀无头骷髅不得额外洗入无头骷髅");
            Assert.IsFalse(
                ContainsEffectTriggeredSince(startIndex, "skill.fall_apart"),
                "他卡被移除时场上大骷髅不得触发散架");
        }

        [Test]
        public void BigSkeleton_SelfKilled_ShufflesFragmentsOnce()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.big_skeleton", sVictimAdjacentSlot);
            PrepareAvatarAttack(12);

            var skullBefore = CountDefInDrawOrBoard("monster.skull_head");
            var headlessBefore = CountDefInDrawOrBoard("monster.headless_skeleton");
            var startIndex = mPipeline.EventLog.Entries.Count;

            var board = mArch.GetModel<BoardModel>();
            var targetUid = board.GetCardUid(sVictimAdjacentSlot);
            Assert.Greater(targetUid, 0);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, targetUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(skullBefore + 1, CountDefInDrawOrBoard("monster.skull_head"), "大骷髅被移除应洗入一张骷髅头");
            Assert.AreEqual(headlessBefore + 1, CountDefInDrawOrBoard("monster.headless_skeleton"), "大骷髅被移除应洗入一张无头骷髅");
            Assert.IsTrue(
                ContainsEffectTriggeredSince(startIndex, "skill.fall_apart"),
                "大骷髅自身被移除应触发散架");
        }

        [Test]
        public void RecombineRemovesFragments_DoesNotTriggerFallApartOnSurvivor()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.big_skeleton", sBigSkeletonSlot);
            SpawnOnBoard("monster.big_skeleton", sSecondBigSkeletonSlot);
            SpawnOnBoard("monster.headless_skeleton", sHeadlessSlot);
            SpawnOnBoard("monster.skull_head", sSkullSlot);

            var skullBefore = CountDefInDrawPile("monster.skull_head");
            var headlessBefore = CountDefInDrawPile("monster.headless_skeleton");
            var startIndex = mPipeline.EventLog.Entries.Count;

            mPipeline.Enqueue(new RotateBoardClockwiseAction(true, "test", "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);

            Assert.IsFalse(
                ContainsEffectTriggeredSince(startIndex, "skill.fall_apart"),
                "重组移除碎片时，场上存活大骷髅不得误触发散架");
            Assert.LessOrEqual(
                CountDefInDrawPile("monster.skull_head"),
                skullBefore + 1,
                "重组至多净增一张大骷髅来源的骷髅头，不得因误触发散架膨胀");
            Assert.LessOrEqual(
                CountDefInDrawPile("monster.headless_skeleton"),
                headlessBefore + 1,
                "重组至多净增一张大骷髅来源的无头骷髅，不得因误触发散架膨胀");
        }

        [Test]
        public void AbsorbBone_AdjacentRemoved_StillTriggers()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_king", SlotId.Board(2));
            SpawnOnBoard("monster.headless_skeleton", SlotId.Board(1));

            var board = mArch.GetModel<BoardModel>();
            var kingUid = board.GetCardUid(SlotId.Board(2));
            var atkBefore = (int)mArch.GetModel<CardRegistry>().Get(kingUid).Stats.GetBase(StatId.Attack);
            var startIndex = mPipeline.EventLog.Entries.Count;
            PrepareAvatarAttack(10);

            var victimUid = board.GetCardUid(SlotId.Board(1));
            Assert.Greater(victimUid, 0);
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, victimUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            var king = mArch.GetModel<CardRegistry>().Get(kingUid);
            Assert.IsTrue(
                ContainsEffectTriggeredSince(startIndex, "skill.absorb_bone")
                || (int)king.Stats.GetBase(StatId.Attack) > atkBefore,
                "吸骨：相邻怪物被移除时应触发或获得攻击");
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

        private void PrepareAvatarAttack(int attack)
        {
            var board = mArch.GetModel<BoardModel>();
            var avatar = mArch.GetModel<CardRegistry>().Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 99);
            avatar.Stats.SetBase(StatId.Hp, 99);
            avatar.Stats.SetBase(StatId.Armor, 0);
            avatar.Stats.SetBase(StatId.Attack, attack);
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

        private int CountDefInDrawOrBoard(string defId)
        {
            return CountDefInDrawPile(defId) + CountDefOnBoard(defId);
        }

        private int CountDefOnBoard(string defId)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var count = 0;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid > 0 && registry.Get(uid).DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private bool ContainsEffectTriggeredSince(int startIndex, string sourceDefId)
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var evt = entries[i];
                if (evt.Type == CoreEventType.EffectTriggered
                    && evt.SourceDefId == sourceDefId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
