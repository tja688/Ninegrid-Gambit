using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 契约测试：R1（Fill→Rotate）/ E1（ClickEmpty 邻接）/ 击杀旋转。
    /// 直接走 PhaseSystem + EventLog，不依赖 CoreOperationFacade。
    /// </summary>
    public sealed class CoreOperationContractTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Attack_Kill_EmitsKillThenFillThenRotate_R1()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            var startIndex = mPipeline.EventLog.Entries.Count;

            var result = mPhase.Attack(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            var events = SliceEvents(startIndex);
            Assert.IsTrue(ContainsType(events, CoreEventType.CardKilled));
            AssertFillBeforeRotate(events);
            Assert.IsTrue(ContainsType(events, CoreEventType.InteractionChanged));
        }

        [Test]
        public void ResolvePostKillBoard_FillBeforeRotate_R1()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            // 先用表现层可信命中清掉怪，再单独测 PostKill 盘面顺序。
            var board = mArch.GetModel<BoardModel>();
            var targetUid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(targetUid, 0);
            var avatarUid = board.AvatarUid.Value;
            Assert.IsTrue(mPhase.ApplyCombatHit(avatarUid, targetUid).Accepted);

            var startIndex = mPipeline.EventLog.Entries.Count;
            var result = mPhase.ResolvePostKillBoard();
            Assert.IsTrue(result.Accepted, result.Reason);

            var events = SliceEvents(startIndex);
            AssertFillBeforeRotate(events);
            Assert.IsTrue(ContainsType(events, CoreEventType.InteractionChanged));
        }

        [Test]
        public void ClickEmpty_Adjacent_RotatesWithR1Order()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            // 把唯一怪挪到远角，邻接格空出。
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            var startIndex = mPipeline.EventLog.Entries.Count;
            var result = mPhase.ClickEmpty(sAdjacentSlot);
            Assert.IsTrue(result.Accepted, result.Reason);

            var events = SliceEvents(startIndex);
            Assert.IsTrue(ContainsType(events, CoreEventType.EmptyClicked));
            AssertFillBeforeRotate(events);
        }

        [Test]
        public void ClickEmpty_NonAdjacent_Rejects_E1()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sFarCornerSlot));

            var result = mPhase.ClickEmpty(sFarCornerSlot);
            Assert.IsFalse(result.Accepted);
            Assert.IsTrue(
                result.Reason.Contains("outside interaction range")
                || result.Reason.Contains("not empty")
                || result.Reason.Contains("not legal"),
                result.Reason);
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }

        private IReadOnlyList<CoreGameEvent> SliceEvents(int startIndex)
        {
            var entries = mPipeline.EventLog.Entries;
            var list = new List<CoreGameEvent>(entries.Count - startIndex);
            for (var i = startIndex; i < entries.Count; i++)
            {
                list.Add(entries[i]);
            }

            return list;
        }

        private static void AssertFillBeforeRotate(IReadOnlyList<CoreGameEvent> events)
        {
            var fillIndex = IndexOfType(events, CoreEventType.SlotsFilled);
            var rotateIndex = IndexOfType(events, CoreEventType.BoardRotated);
            Assert.GreaterOrEqual(fillIndex, 0, "Expected SlotsFilled");
            Assert.GreaterOrEqual(rotateIndex, 0, "Expected BoardRotated");
            Assert.Less(fillIndex, rotateIndex, "R1: Fill must precede Rotate");
        }

        private static bool ContainsType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            return IndexOfType(events, type) >= 0;
        }

        private static int IndexOfType(IReadOnlyList<CoreGameEvent> events, CoreEventType type)
        {
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
