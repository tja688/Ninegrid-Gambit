using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #217 验收测试：
    /// 盘面置换带走离巢 Avatar（旋转、双卡交换、按格移卡）。
    /// </summary>
    public class DisplacedAvatarBoardMotionRegressionTests
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
        public void AvatarOnOuterRing_RotateClockwise_DisplacesAvatarToNextRingSlot_EmitsAvatarMoved()
        {
            // 夹具把 Avatar 放到外圈格 1
            var avatar = CreateAvatarOnBoard(SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value);

            // 执行顺时针旋转
            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new RotateBoardClockwiseAction(true, "test.rotator", "test.cause"), recordedEvents);

            // 验收：环序 1 -> 2
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "顺时针旋转后 Avatar 应从格 1 移动至格 2");

            // 验收：发出 AvatarMoved 事件
            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(avatarMoved, "必须发出 AvatarMoved 事件");
            Assert.AreEqual(avatar.Uid, avatarMoved.CardUid);
            Assert.AreEqual(SlotId.Board(1), avatarMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(2), avatarMoved.ToSlot);
            Assert.AreEqual("test.rotator", avatarMoved.SourceDefId);
            Assert.AreEqual("test.cause", avatarMoved.Cause);

            // 再次顺时针旋转：2 -> 3
            recordedEvents.Clear();
            RunAndCollectEvents(new RotateBoardClockwiseAction(true), recordedEvents);
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value, "再次顺时针旋转后 Avatar 应从格 2 移动至格 3");
            var secondMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(secondMoved);
            Assert.AreEqual(SlotId.Board(2), secondMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(3), secondMoved.ToSlot);
        }

        [Test]
        public void AvatarOnOuterRing_RotateCounterClockwise_DisplacesAvatarToPreviousRingSlot_EmitsAvatarMoved()
        {
            // 夹具把 Avatar 放到外圈格 1
            var avatar = CreateAvatarOnBoard(SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            // 逆时针旋转：1 -> 4
            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new RotateBoardClockwiseAction(false, "test.rotator", "test.ccw"), recordedEvents);

            Assert.AreEqual(SlotId.Board(4), board.AvatarSlot.Value, "逆时针旋转后 Avatar 应从格 1 移动至格 4");

            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(avatarMoved, "必须发出 AvatarMoved 事件");
            Assert.AreEqual(avatar.Uid, avatarMoved.CardUid);
            Assert.AreEqual(SlotId.Board(1), avatarMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(4), avatarMoved.ToSlot);
        }

        [Test]
        public void AvatarAtCenter_Rotate_DoesNotDisplaceAvatar_NoAvatarMovedEvent()
        {
            // Avatar 在中心格 5
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var board = mArch.GetModel<BoardModel>();

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new RotateBoardClockwiseAction(true), recordedEvents);

            // 验收：中心格仍不旋转
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "中心格 Avatar 不随外圈旋转移动");
            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNull(avatarMoved, "中心格 Avatar 不发生移动，不发出 AvatarMoved 事件");
        }

        [Test]
        public void AvatarOnSwapSlot_LeftSlot_DisplacesAvatarToRightSlot_AndRightCardToLeftSlot_NoOverlap()
        {
            // Avatar 在格 1，Monster 卡在格 2
            var avatar = CreateAvatarOnBoard(SlotId.Board(1));
            var monster = CreateMonsterOnBoard("monster.dummy", SlotId.Board(2));
            var board = mArch.GetModel<BoardModel>();

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new SwapBoardSlotsAction(SlotId.Board(1), SlotId.Board(2), "test.swap", "test.cause"), recordedEvents);

            // 验收：怪物被移动到格 1
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(1)), "格 2 的卡应移动到格 1");
            Assert.AreEqual(SlotId.Board(1), monster.Slot.Value);

            // 验收：Avatar 被带到对格（格 2）
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "Avatar 应被带到对格 2");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(2)), "格 2 无场上卡（由 Avatar 独占），不与卡叠在同一格");

            // 验收：发出 AvatarMoved 事件与 CardMoved 事件
            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(avatarMoved, "必须发出 AvatarMoved 事件");
            Assert.AreEqual(avatar.Uid, avatarMoved.CardUid);
            Assert.AreEqual(SlotId.Board(1), avatarMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(2), avatarMoved.ToSlot);
            Assert.AreEqual("test.swap", avatarMoved.SourceDefId);

            var cardMoved = recordedEvents.Find(e => e.Type == CoreEventType.CardMoved && e.CardUid == monster.Uid);
            Assert.IsNotNull(cardMoved, "怪物卡发出 CardMoved 事件");
            Assert.AreEqual(SlotId.Board(2), cardMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(1), cardMoved.ToSlot);
        }

        [Test]
        public void AvatarOnSwapSlot_RightSlot_DisplacesAvatarToLeftSlot_AndLeftCardToRightSlot_NoOverlap()
        {
            // Avatar 在格 2，Monster 卡在格 1
            var avatar = CreateAvatarOnBoard(SlotId.Board(2));
            var monster = CreateMonsterOnBoard("monster.dummy", SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new SwapBoardSlotsAction(SlotId.Board(1), SlotId.Board(2)), recordedEvents);

            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(2)), "格 1 的卡应移动到格 2");
            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value, "Avatar 应被带到对格 1");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(1)), "格 1 无场上卡，不与卡重叠");

            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(avatarMoved);
            Assert.AreEqual(SlotId.Board(2), avatarMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(1), avatarMoved.ToSlot);
        }

        [Test]
        public void AvatarOnDifferentSlot_SwapDoesNotDisplaceAvatar()
        {
            // Avatar 在格 3，Monster A 在格 1，Monster B 在格 2
            var avatar = CreateAvatarOnBoard(SlotId.Board(3));
            var monsterA = CreateMonsterOnBoard("monster.a", SlotId.Board(1));
            var monsterB = CreateMonsterOnBoard("monster.b", SlotId.Board(2));
            var board = mArch.GetModel<BoardModel>();

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new SwapBoardSlotsAction(SlotId.Board(1), SlotId.Board(2)), recordedEvents);

            Assert.AreEqual(monsterB.Uid, board.GetCardUid(SlotId.Board(1)));
            Assert.AreEqual(monsterA.Uid, board.GetCardUid(SlotId.Board(2)));
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value, "Avatar 仍在格 3");

            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNull(avatarMoved, "非涉及格的交换不应触发 AvatarMoved");
        }

        [Test]
        public void AvatarAtCenter_SwapCenterWithOuterSlot_DisplacesAvatarToOuterSlot()
        {
            // Avatar 在中心格 5，Monster 卡在格 1
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateMonsterOnBoard("monster.dummy", SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new SwapBoardSlotsAction(SlotId.Center, SlotId.Board(1)), recordedEvents);

            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Center), "怪物卡被换入中心格 5");
            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value, "Avatar 被换出至外圈格 1");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(1)), "外圈格 1 无场上卡，由 Avatar 占格");

            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(avatarMoved);
            Assert.AreEqual(SlotId.Center, avatarMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(1), avatarMoved.ToSlot);
        }

        [Test]
        public void MoveCardAction_BoardCardMovedToAvatarSlot_DisplacesAvatarToOriginSlot()
        {
            // Avatar 在格 1，Monster 在格 2
            var avatar = CreateAvatarOnBoard(SlotId.Board(1));
            var monster = CreateMonsterOnBoard("monster.mover", SlotId.Board(2));
            var board = mArch.GetModel<BoardModel>();

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new MoveCardAction(monster.Uid, SlotId.Board(1), "test.move", "test.cause"), recordedEvents);

            // 验收：Monster 占格 1
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(1)), "Monster 应移至格 1");
            // 验收：Avatar 被带到 Monster 原格（格 2）
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value, "Avatar 应被带走至格 2，避免与 Monster 重叠");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(2)), "格 2 无场上卡");

            // 验收：发出 AvatarMoved 事件
            var avatarMoved = recordedEvents.Find(e => e.Type == CoreEventType.AvatarMoved);
            Assert.IsNotNull(avatarMoved, "必须发出 AvatarMoved 事件");
            Assert.AreEqual(avatar.Uid, avatarMoved.CardUid);
            Assert.AreEqual(SlotId.Board(1), avatarMoved.FromSlot);
            Assert.AreEqual(SlotId.Board(2), avatarMoved.ToSlot);
        }

        [Test]
        public void MoveCardAction_OffBoardCardMovedToAvatarSlot_ThrowsInvalidOperation()
        {
            // Avatar 在格 1
            CreateAvatarOnBoard(SlotId.Board(1));
            var registry = mArch.GetModel<CardRegistry>();
            var unplacedCard = registry.Create("monster.unplaced", CardKind.Monster);
            var board = mArch.GetModel<BoardModel>();

            var action = new MoveCardAction(unplacedCard.Uid, SlotId.Board(1));
            var context = new GameActionContext(mArch, new EventLog(), 1, 0);

            // 直接 Apply 抛出 InvalidOperationException
            Assert.Throws<InvalidOperationException>(() =>
            {
                action.Apply(context);
            });

            // 经由管线执行时被 ADR-0047 熔断保护遏制，卡牌绝不落入 Avatar 占格，格 1 保持为 Avatar 独占
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(action);
            Assert.AreEqual(0, board.GetCardUid(SlotId.Board(1)), "未在盘面的卡不得落入 Avatar 占格");
            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value, "Avatar 仍在格 1");
            Assert.IsTrue(pipeline.EventLog.Contains(CoreEventType.PipelineFaultContained), "管线记录了熔断保护事件");
        }

        [Test]
        public void SelectedCardsTarget_ExcludesAvatarByDefault()
        {
            // 验收：SelectedCards 目标解析器默认排除 Avatar，现有「交换」卡不会把玩家变成可选目标
            var avatar = CreateAvatarOnBoard(SlotId.Board(1));
            var monster1 = CreateMonsterOnBoard("monster.dummy_1", SlotId.Board(2));
            var monster2 = CreateMonsterOnBoard("monster.dummy_2", SlotId.Board(3));

            var targetDsl = new EffectDslNode(new Dictionary<string, object>
            {
                { "atom", "SelectedCards" },
                { "zone", "Board" },
                { "count", 2 }
            });

            var targetAtom = new SelectedCardsTarget();
            targetAtom.Configure(targetDsl);

            // 1. 尝试选择 Avatar 与 Monster 1：Avatar 被过滤，导致有效目标数不足 2，返回空列表（拒绝使用）
            var useItemWithAvatar = new UseItemAction(999, new[] { avatar.Uid, monster1.Uid });
            var triggerContext1 = new TriggerContext(
                TriggerPoint.AfterAction,
                TriggerTiming.Post,
                useItemWithAvatar,
                Array.Empty<CoreGameEvent>(),
                null);
            var runtimeContext1 = new EffectRuntimeContext(mArch, null, triggerContext1);
            var resolvedWithAvatar = targetAtom.Resolve(runtimeContext1);
            Assert.AreEqual(0, resolvedWithAvatar.Count, "包含 Avatar 时因 Avatar 被排除导致目标数量不足，返回空");

            // 2. 选择两张合法怪物卡：正常解析出两张怪物
            var useItemWithMonsters = new UseItemAction(999, new[] { monster1.Uid, monster2.Uid });
            var triggerContext2 = new TriggerContext(
                TriggerPoint.AfterAction,
                TriggerTiming.Post,
                useItemWithMonsters,
                Array.Empty<CoreGameEvent>(),
                null);
            var runtimeContext2 = new EffectRuntimeContext(mArch, null, triggerContext2);
            var resolvedMonsters = targetAtom.Resolve(runtimeContext2);
            var list = new List<int>(resolvedMonsters);
            Assert.AreEqual(2, list.Count);
            Assert.IsTrue(list.Contains(monster1.Uid));
            Assert.IsTrue(list.Contains(monster2.Uid));
            Assert.IsFalse(list.Contains(avatar.Uid));
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

        private CardInstance CreateMonsterOnBoard(string defId, SlotId slot)
        {
            var monster = mArch.GetModel<CardRegistry>().Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 10);
            monster.Stats.SetBase(StatId.Hp, 10);
            monster.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private void RunAndCollectEvents(GameAction action, List<CoreGameEvent> collected)
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var startCount = pipeline.EventLog.Entries.Count;
            pipeline.Execute(action);
            for (var i = startCount; i < pipeline.EventLog.Entries.Count; i++)
            {
                collected.Add(pipeline.EventLog.Entries[i]);
            }
        }
    }
}
