using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #216 验收测试：
    /// 中心格是拓扑：不补牌、环判定、发牌避开当前 Avatar 格。
    /// </summary>
    public class CenterSlotTopologyRefillRegressionTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AvatarOnOuterRing_CenterEmpty_RefillDoesNotFillCenterOrAvatarSlot_FillsOtherOuterSlots()
        {
            // 夹具把 Avatar 放到外圈（格 1），中心格（格 5）留空
            var avatar = CreateAvatarOnBoard(SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            Assert.IsTrue(board.IsEmpty(SlotId.Center), "中心格初始为空");
            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value, "Avatar 在外圈格 1");

            // 放入 10 张测试牌入抽牌堆
            for (var i = 0; i < 10; i++)
            {
                QueueRefillCard("monster.test_" + i, 10);
            }

            // 执行补牌管线
            Run(new FillEmptySlotsAction());

            // 验收：中心格留空时，补牌绝不往中心格发牌
            Assert.IsTrue(board.IsEmpty(SlotId.Center), "补牌不得往中心格发牌，中心格保持为空");

            // 验收：补牌绝不发到 Avatar 当前格（格 1）
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(1)), "Avatar 当前格不得被发牌占用");
            Assert.AreEqual(avatar.Uid, board.AvatarUid.Value, "Avatar 仍在格 1");

            // 验收：其余 7 个外圈格均被补牌填充
            var outerSlots = new[]
            {
                SlotId.Board(2),
                SlotId.Board(3),
                SlotId.Board(6),
                SlotId.Board(9),
                SlotId.Board(8),
                SlotId.Board(7),
                SlotId.Board(4)
            };

            for (var i = 0; i < outerSlots.Length; i++)
            {
                var s = outerSlots[i];
                Assert.IsFalse(board.IsEmpty(s), $"外圈空格 {s} 应正常被补满");
                Assert.AreNotEqual(0, board.GetCardUid(s));
            }
        }

        [Test]
        public void PrioritySlotOnCenter_RefillIgnoresCenterPriority()
        {
            CreateAvatarOnBoard(SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            QueueRefillCard("monster.test_priority", 10);

            // 即使调用者指定 PrioritySlot 为中心格 5，也绝不补入中心格
            Run(new FillEmptySlotsAction(SlotId.Center));

            Assert.IsTrue(board.IsEmpty(SlotId.Center), "中心格禁止补牌，即便作为 PrioritySlot 传入也不补");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(1)), "Avatar 当前格不补");
            Assert.IsFalse(board.IsEmpty(SlotId.Board(2)), "牌应补入下一个合法外圈格（格 2）");
        }

        [Test]
        public void BoardStabilization_PendingEmptySlotCount_IgnoresCenterSlot()
        {
            CreateAvatarOnBoard(SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            // 填满除格 1（Avatar）与格 5（Center）之外的所有外圈格
            var outerSlots = new[]
            {
                SlotId.Board(2),
                SlotId.Board(3),
                SlotId.Board(6),
                SlotId.Board(9),
                SlotId.Board(8),
                SlotId.Board(7),
                SlotId.Board(4)
            };

            for (var i = 0; i < outerSlots.Length; i++)
            {
                var registry = mArch.GetModel<CardRegistry>();
                var card = registry.Create("monster.dummy", CardKind.Monster);
                board.PlaceCard(card, outerSlots[i]);
            }

            Assert.IsTrue(board.IsEmpty(SlotId.Center), "中心格为空");

            var stab = mArch.GetSystem<IBoardStabilizationSystem>();
            Assert.AreEqual(0, stab.PendingEmptySlotCount, "所有外圈格已满且 Avatar 在格 1，待补空格数应为 0（中心空格不计入）");
            Assert.IsFalse(stab.NeedsRefill, "不应触发补牌需求");
        }

        [Test]
        public void GroundSlotTopology_RingChecksAndIndices_CenterIsNotOnOuterRing()
        {
            // 环判定：中心格 5 不在外圈环上
            Assert.IsFalse(GroundSlotTopology.IsOuterRing(5), "格 5 不应被判定为外圈环上格");
            Assert.IsFalse(GroundSlotTopology.IsOuterRing(0), "格 0 无效，不应判定为外圈环");
            Assert.IsFalse(GroundSlotTopology.IsOuterRing(10), "格 10 无效，不应判定为外圈环");

            // 外圈 8 格必须在环上
            var ringSlots = new[] { 1, 2, 3, 6, 9, 8, 7, 4 };
            for (var i = 0; i < ringSlots.Length; i++)
            {
                var s = ringSlots[i];
                Assert.IsTrue(GroundSlotTopology.IsOuterRing(s), $"外圈格 {s} 应为外圈环上格");
                Assert.AreEqual(i, GroundSlotTopology.GetClockwiseRingIndex(s), $"外圈格 {s} 环索引应为 {i}");
            }

            // 中心格环索引应为 -1
            Assert.AreEqual(-1, GroundSlotTopology.GetClockwiseRingIndex(5), "格 5 环索引必须为 -1");

            // 顺时针/逆时针推进格 5 应原地不动
            Assert.AreEqual(5, GroundSlotTopology.GetClockwiseRingTargetSlot(5, clockwise: true));
            Assert.AreEqual(5, GroundSlotTopology.GetClockwiseRingTargetSlot(5, clockwise: false));

            // IsCenter 谓词
            Assert.IsTrue(GroundSlotTopology.IsCenter(5));
            Assert.IsFalse(GroundSlotTopology.IsCenter(1));

            // QueryRelation 检查
            var centerRelation = GroundSlotTopology.QueryRelation(5, 5);
            Assert.AreEqual(0, (int)(centerRelation & GroundSlotRelation.OuterRing), "中心格 relation 不应包含 OuterRing");
        }

        [Test]
        public void SlotId_Center_Properties()
        {
            Assert.AreEqual(5, SlotId.CenterIndex);
            Assert.AreEqual(5, SlotId.Center.Index);
            Assert.IsTrue(SlotId.Center.IsCenter);
            Assert.IsTrue(SlotId.Center.IsBoardSlot);
            Assert.AreEqual(SlotId.Board(5), SlotId.Center);
            Assert.IsFalse(SlotId.Board(1).IsCenter);
            Assert.IsFalse(SlotId.Avatar.IsCenter);
            Assert.IsFalse(SlotId.None.IsCenter);
        }

        [Test]
        public void CardPlacementGating_SeparatesCenterAndAvatarSlot()
        {
            // 拓扑发牌禁位：中心格 5 拓扑不可放
            Assert.IsFalse(CardSlotAnchorUtility.IsPlaceableGroundSlot(5), "中心格 5 拓扑不可发牌");
            Assert.IsTrue(CardSlotAnchorUtility.IsPlaceableGroundSlot(1), "格 1 拓扑可发牌");
            Assert.AreEqual(5, CardSlotAnchorUtility.CenterGroundSlotNumber);
            Assert.AreEqual(5, CardSlotAnchorUtility.ForbiddenGroundDealSlotNumber);

            // 表现层占格索引：拓扑不可放与动态占用分开
            var index = new GroundOccupancyIndex();
            Assert.IsFalse(index.IsPlaceable(5), "空中心格 IsPlaceable 应为 false（拓扑禁发牌）");
            Assert.IsTrue(index.IsPlaceable(1), "合格空外圈格 IsPlaceable 应为 true");

            // Avatar 登记到格 1
            index.TryRegister(1, 999);
            Assert.IsFalse(index.IsPlaceable(1), "Avatar 占用格 1 后 IsPlaceable 应为 false（动态已占用）");
            Assert.IsTrue(index.IsPlaceable(2), "空外圈格 2 仍可放置");
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private void QueueRefillCard(string defId, int hp = 10)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var card = registry.Create(defId, CardKind.Monster);
            card.Stats.SetBase(StatId.MaxHp, hp);
            card.Stats.SetBase(StatId.Hp, hp);
            Run(new ShuffleCardIntoDrawPileAction(card.Uid, true, "test.refill"));
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}
