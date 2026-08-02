using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class AvatarWalkPathfinderTests
    {
        private IArchitecture mArch;
        private BoardModel mBoard;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });
            mBoard = mArch.GetModel<BoardModel>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void SameSlot_EmptyPath_True()
        {
            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard, SlotId.Board(5), SlotId.Board(5), path));
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void OrthogonalNeighbor_SingleStep()
        {
            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard, SlotId.Board(5), SlotId.Board(2), path));
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(2, path[0].Index);
        }

        [Test]
        public void Diagonal_RequiresTwoSteps()
        {
            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard, SlotId.Board(5), SlotId.Board(1), path));
            Assert.AreEqual(2, path.Count);
            Assert.IsTrue(path[0].IsAdjacentTo(SlotId.Board(5)));
            Assert.AreEqual(1, path[path.Count - 1].Index);
        }

        [Test]
        public void OccupiedWaypoint_BlocksPath()
        {
            var draft = new CardDraft("monster.block", CardKind.Monster) { MaxHp = 1, Attack = 0 };
            var card = draft.Create(mArch.GetModel<CardRegistry>());
            mBoard.PlaceCard(card, SlotId.Board(2));
            mBoard.PlaceCard(draft.Create(mArch.GetModel<CardRegistry>()), SlotId.Board(4));

            // 兼容重载：无软占回退 → 1 只能经 2 或 4 到 5，皆被占则无路。
            var path = new List<SlotId>();
            Assert.IsFalse(AvatarWalkPathfinder.TryFindPath(
                mBoard, SlotId.Board(1), SlotId.Board(5), path));
        }

        [Test]
        public void IconDestination_IsReachable_WhenEmpty()
        {
            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard,
                SlotId.Board(5),
                SlotId.Board(1),
                path,
                isDestination: slot => slot.Index == 1,
                isSoftBlocked: _ => false));
            Assert.AreEqual(1, path[path.Count - 1].Index);
        }

        [Test]
        public void PreferEmpty_DetoursAroundSoftBlocked_WhenEmptyPathExists()
        {
            // 5→1：短途经常含 2；把 2 标软占后应绕开（经 4 或 6 等空格）。
            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard,
                SlotId.Board(5),
                SlotId.Board(1),
                path,
                isDestination: slot => slot.Index == 1,
                isSoftBlocked: slot => slot.Index == 2));
            Assert.Greater(path.Count, 0);
            for (var i = 0; i < path.Count - 1; i++)
            {
                Assert.AreNotEqual(2, path[i].Index, "must not use soft-blocked waypoint when empty detour exists");
            }

            Assert.AreEqual(1, path[path.Count - 1].Index);
        }

        [Test]
        public void SoftFallback_AllowsCrossingOccupied_WhenNoEmptyPath()
        {
            var draft = new CardDraft("monster.block", CardKind.Monster) { MaxHp = 1, Attack = 0 };
            mBoard.PlaceCard(draft.Create(mArch.GetModel<CardRegistry>()), SlotId.Board(2));
            mBoard.PlaceCard(draft.Create(mArch.GetModel<CardRegistry>()), SlotId.Board(4));

            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard,
                SlotId.Board(1),
                SlotId.Board(5),
                path,
                isDestination: slot => slot.Index == 5,
                isSoftBlocked: _ => false));
            Assert.AreEqual(5, path[path.Count - 1].Index);
            Assert.IsTrue(
                path.Exists(s => s.Index == 2) || path.Exists(s => s.Index == 4),
                "fallback must step on an occupied waypoint");
        }

        [Test]
        public void SoftFallback_AllowsCrossingOtherIcon_WhenNoEmptyPath()
        {
            var soft = new HashSet<int> { 2, 3, 4, 6, 7, 8, 9 };
            var path = new List<SlotId>();
            Assert.IsTrue(AvatarWalkPathfinder.TryFindPath(
                mBoard,
                SlotId.Board(5),
                SlotId.Board(1),
                path,
                isDestination: slot => slot.Index == 1,
                isSoftBlocked: slot => soft.Contains(slot.Index)));
            Assert.AreEqual(1, path[path.Count - 1].Index);
            Assert.IsTrue(path.Exists(s => soft.Contains(s.Index) && s.Index != 1));
        }

        [Test]
        public void OccupiedNonIcon_Destination_Rejected_EvenWithSoftFallback()
        {
            var draft = new CardDraft("monster.block", CardKind.Monster) { MaxHp = 1, Attack = 0 };
            mBoard.PlaceCard(draft.Create(mArch.GetModel<CardRegistry>()), SlotId.Board(2));

            var path = new List<SlotId>();
            Assert.IsFalse(AvatarWalkPathfinder.TryFindPath(
                mBoard,
                SlotId.Board(5),
                SlotId.Board(2),
                path,
                isDestination: _ => false,
                isSoftBlocked: _ => false));
        }

        [Test]
        public void MoveAvatar_AdjacentOccupied_Allowed_InRoomChoice_ForSoftFallback()
        {
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartWalkSandboxNode().Accepted);
            var draft = new CardDraft("monster.block", CardKind.Monster) { MaxHp = 1, Attack = 0 };
            mBoard.PlaceCard(draft.Create(mArch.GetModel<CardRegistry>()), SlotId.Board(2));

            Assert.IsTrue(phase.MoveAvatar(SlotId.Board(2)).Accepted);
            Assert.AreEqual(2, mBoard.AvatarSlot.Value.Index);
            Assert.IsFalse(mBoard.IsEmpty(SlotId.Board(2)), "card remains; AvatarSlot shares cell");
        }

        [Test]
        public void MoveAvatar_AdjacentEmpty_InRoomChoice_Accepted()
        {
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartWalkSandboxNode().Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, phase.CurrentPhase);
            Assert.AreEqual(5, mBoard.AvatarSlot.Value.Index);

            Assert.IsTrue(phase.MoveAvatar(SlotId.Board(2)).Accepted);
            Assert.AreEqual(2, mBoard.AvatarSlot.Value.Index);
        }

        [Test]
        public void MoveAvatar_Rejected_InInteractionLoop()
        {
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, phase.CurrentPhase);

            Assert.IsFalse(phase.MoveAvatar(SlotId.Board(2)).Accepted);
        }

        [Test]
        public void StartWalkSandboxNode_EndsInRoomChoice_WithoutReward()
        {
            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.StartWalkSandboxNode().Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, phase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.None, mArch.GetModel<PendingChoiceModel>().Kind.Value);

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                if (i == mBoard.AvatarSlot.Value.Index)
                {
                    continue;
                }

                Assert.IsTrue(mBoard.IsEmpty(SlotId.Board(i)), "slot " + i);
            }
        }
    }
}
