using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #10 idle 合法性缝：Flow 对 BoardModel 裁决，不依赖 Cards 占格镜像。
    /// </summary>
    public sealed class BoardIntentLegalityTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Explore_AdjacentEmpty_IsLegal()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            string reason;
            Assert.IsTrue(BoardIntentLegality.TryExplainExplore(mArch, sAdjacentSlot.Index, out reason));
            Assert.IsNull(reason);
        }

        [Test]
        public void Explore_FarEmpty_IsIllegal_NotAdjacent()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sFarCornerSlot));

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainExplore(mArch, sFarCornerSlot.Index, out reason));
            StringAssert.Contains("notAdjacent", reason);
        }

        [Test]
        public void Explore_OccupiedSlot_IsIllegal_UsesBoardModelNotCards()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainExplore(mArch, sAdjacentSlot.Index, out reason));
            StringAssert.Contains("notEmptyOrAvatar", reason);
        }

        [Test]
        public void Attack_AdjacentMonster_IsLegal()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            string reason;
            Assert.IsTrue(BoardIntentLegality.TryExplainAttack(mArch, sAdjacentSlot.Index, out reason));
            Assert.IsNull(reason);
        }

        [Test]
        public void Attack_EmptyAdjacent_IsIllegal()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sFarCornerSlot);
            Assert.IsTrue(mArch.GetModel<BoardModel>().IsEmpty(sAdjacentSlot));

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainAttack(mArch, sAdjacentSlot.Index, out reason));
            StringAssert.Contains("emptySlot", reason);
        }

        [Test]
        public void UseItem_MissingUid_IsIllegal()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainUseItem(mArch, 99999, null, null, out reason));
            StringAssert.Contains("itemMissing", reason);
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
    }
}
