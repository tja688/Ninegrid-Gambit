using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #220 验收测试：
    /// 归位倒计时：落地后三次占格变化回中心（ADR-0057）。
    /// </summary>
    public class AvatarHomingCountdownRegressionTests
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
        public void SwapAvatarWithCard_LandsOnOuterRing_InitializesCountToZero_AndEntersOffHome()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateMonsterOnBoard("monster.test.swappee", SlotId.Board(2));
            var board = mArch.GetModel<BoardModel>();

            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
            Assert.IsFalse(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);

            // 执行原子换位动作至格 2
            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));

            // 验收：落地后计数为 0，进入离巢状态
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value, "换位落地到外圈后应处于离巢状态");
            Assert.AreEqual(0, board.AvatarHomingSteps.Value, "落地那一刻计数应为 0（不计入倒计时）");
        }

        [Test]
        public void RotateThreeTimes_ReturnsAvatarToCenter_EmptyCenter()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateMonsterOnBoard("monster.test.swappee", SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            // 换位至格 1（中心空格留给换出的卡在格 5，这里我们把格 5 的卡移走制造中心空格场景）
            Run(new SwapAvatarWithCardAction(SlotId.Board(1)));
            board.ClearSlot(SlotId.Center);

            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);
            Assert.IsTrue(board.IsEmpty(SlotId.Center), "中心格为空");

            // 旋转 1 次：顺时针 1 -> 2
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value);
            Assert.AreEqual(1, board.AvatarHomingSteps.Value, "第 1 次占格变化计数为 1");

            // 旋转 2 次：顺时针 2 -> 3
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value);
            Assert.AreEqual(2, board.AvatarHomingSteps.Value, "第 2 次占格变化计数为 2");

            // 旋转 3 次：顺时针 3 -> 6，触发归位动作回中心格 5
            Run(new RotateBoardClockwiseAction(true));

            // 验收：Avatar 移回中心格，退出离巢状态，计数清 0
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "满 3 次后 Avatar 应归位到中心格 5");
            Assert.AreEqual(SlotId.Center, avatar.Slot.Value);
            Assert.AreEqual(ZoneId.Avatar, avatar.Zone.Value);
            Assert.IsFalse(board.IsAvatarOffHome.Value, "归位后不在离巢状态");
            Assert.AreEqual(0, board.AvatarHomingSteps.Value, "归位后计数应清零");
        }

        [Test]
        public void RotateThreeTimes_ReturnsAvatarToCenter_OccupiedCenter_SwapsWithOccupant()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monsterA = CreateMonsterOnBoard("monster.test.swappee", SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            // 换位至格 1：Avatar 到格 1，monsterA 到中心格 5
            Run(new SwapAvatarWithCardAction(SlotId.Board(1)));

            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value);
            Assert.AreEqual(monsterA.Uid, board.GetCardUid(SlotId.Center), "中心格 5 此时由 monsterA 占用");
            Assert.IsTrue(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);

            // 旋转 1 次：顺时针外圈 1 -> 2；中心格 5 不随外圈旋转
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.AreEqual(monsterA.Uid, board.GetCardUid(SlotId.Center));
            Assert.AreEqual(1, board.AvatarHomingSteps.Value);

            // 旋转 2 次：顺时针外圈 2 -> 3
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value);
            Assert.AreEqual(monsterA.Uid, board.GetCardUid(SlotId.Center));
            Assert.AreEqual(2, board.AvatarHomingSteps.Value);

            // 旋转 3 次：顺时针外圈 3 -> 6，触发与中心格 monsterA 原子换位
            Run(new RotateBoardClockwiseAction(true));

            // 验收：Avatar 回到中心格 5，monsterA 被换至格 6
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "Avatar 应归位至中心格 5");
            Assert.AreEqual(0, board.GetCardUid(SlotId.Center), "中心格不应有场上卡占用（人卡解耦）");
            Assert.AreEqual(monsterA.Uid, board.GetCardUid(SlotId.Board(6)), "monsterA 应被换位至格 6");
            Assert.AreEqual(SlotId.Board(6), monsterA.Slot.Value);
            Assert.AreEqual(ZoneId.Board, monsterA.Zone.Value);

            Assert.IsFalse(board.IsAvatarOffHome.Value, "归位后退出离巢状态");
            Assert.AreEqual(0, board.AvatarHomingSteps.Value, "归位后计数清零");
        }

        [Test]
        public void DisplacementsAcrossDifferentActions_ReachThree_TriggersReturn()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster1 = CreateMonsterOnBoard("monster.test.m1", SlotId.Board(1));
            var monster8 = CreateMonsterOnBoard("monster.test.m8", SlotId.Board(8));
            var monster7 = CreateMonsterOnBoard("monster.test.m7", SlotId.Board(7));
            var board = mArch.GetModel<BoardModel>();

            // 换位至格 1：count = 0
            Run(new SwapAvatarWithCardAction(SlotId.Board(1)));
            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);

            // 位移 1：旋转外圈顺时针，Avatar 1 -> 2：count = 1
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.AreEqual(1, board.AvatarHomingSteps.Value);

            // 位移 2：双卡交换格 2 与格 8，Avatar 被带到格 8：count = 2
            Run(new SwapBoardSlotsAction(SlotId.Board(2), SlotId.Board(8)));
            Assert.AreEqual(SlotId.Board(8), board.AvatarSlot.Value);
            Assert.AreEqual(2, board.AvatarHomingSteps.Value);

            // 位移 3：按格移卡把 monster7 移入格 8，置换带走 Avatar 至格 7：count 达到 3 -> 触发归位回中心
            Run(new MoveCardAction(monster7.Uid, SlotId.Board(8)));

            // 验收：Avatar 回到中心格 5
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value, "三次不同位移后 Avatar 归位回中心格");
            Assert.IsFalse(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);
        }

        [Test]
        public void SwapBetweenUnrelatedSlots_DoesNotIncrementCountdown()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster1 = CreateMonsterOnBoard("monster.test.m1", SlotId.Board(1));
            var monster2 = CreateMonsterOnBoard("monster.test.m2", SlotId.Board(2));
            var monster3 = CreateMonsterOnBoard("monster.test.m3", SlotId.Board(3));
            var board = mArch.GetModel<BoardModel>();

            // 换位至格 2
            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);

            // 旋转 1 次：2 -> 3，count = 1
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value);
            Assert.AreEqual(1, board.AvatarHomingSteps.Value);

            // 交换格 1 与格 4（与 Avatar 当前格 3 无关）
            Run(new SwapBoardSlotsAction(SlotId.Board(1), SlotId.Board(4)));

            // 验收：Avatar 仍留在格 3，计数不增加
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value, "Avatar 仍在格 3");
            Assert.AreEqual(1, board.AvatarHomingSteps.Value, "无关格交换不增加倒计时计数");
            Assert.IsTrue(board.IsAvatarOffHome.Value);
        }

        [Test]
        public void SecondAvatarSwap_ResetsCountToZero_AndRestartsCountdown()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster1 = CreateMonsterOnBoard("monster.test.m1", SlotId.Board(1));
            var monster7 = CreateMonsterOnBoard("monster.test.m7", SlotId.Board(7));
            var board = mArch.GetModel<BoardModel>();

            // 第 1 次换位至格 1
            Run(new SwapAvatarWithCardAction(SlotId.Board(1)));
            Assert.AreEqual(SlotId.Board(1), board.AvatarSlot.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);

            // 旋转 2 次：1 -> 2 -> 3，count = 2
            Run(new RotateBoardClockwiseAction(true));
            Run(new RotateBoardClockwiseAction(true));
            Assert.AreEqual(SlotId.Board(3), board.AvatarSlot.Value);
            Assert.AreEqual(2, board.AvatarHomingSteps.Value);

            // 在离巢状态下再次执行原子换位至格 7
            Run(new SwapAvatarWithCardAction(SlotId.Board(7)));

            // 验收：Avatar 在格 7，计数重置为 0，从新落地起算
            Assert.AreEqual(SlotId.Board(7), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value, "再次原子换位后计数应重置为 0 从新落地起算");

            // 随后从格 7 旋转 3 次：7 -> 4 -> 1 -> 2 (逆时针演示)
            Run(new RotateBoardClockwiseAction(false));
            Assert.AreEqual(1, board.AvatarHomingSteps.Value);
            Run(new RotateBoardClockwiseAction(false));
            Assert.AreEqual(2, board.AvatarHomingSteps.Value);
            Run(new RotateBoardClockwiseAction(false));

            // 验收：满 3 次后归位回中心
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
            Assert.IsFalse(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);
        }

        [Test]
        public void DirectSwapToCenter_ClearsOffHomeAndCountImmediately()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster2 = CreateMonsterOnBoard("monster.test.m2", SlotId.Board(2));
            var board = mArch.GetModel<BoardModel>();

            // 换位至格 2
            Run(new SwapAvatarWithCardAction(SlotId.Board(2)));
            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value);

            // 直接再次换位回中心格 5
            Run(new SwapAvatarWithCardAction(SlotId.Center));

            // 验收：立即退出离巢状态，计数清零
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);
            Assert.IsFalse(board.IsAvatarOffHome.Value);
            Assert.AreEqual(0, board.AvatarHomingSteps.Value);
        }

        [Test]
        public void StartNode_ClearsOffHomeAndResetsCountdown_AvatarAtCenter()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Board(2));
            var board = mArch.GetModel<BoardModel>();
            board.StartAvatarOffHomeCountdown();
            board.AvatarHomingSteps.Value = 2;

            Assert.AreEqual(SlotId.Board(2), board.AvatarSlot.Value);
            Assert.IsTrue(board.IsAvatarOffHome.Value);
            Assert.AreEqual(2, board.AvatarHomingSteps.Value);

            // 开启新节点
            var phaseSystem = mArch.GetSystem<IPhaseSystem>();
            phaseSystem.StartNode(NodeDeckOptions.CreateDefaultBattle());

            // 验收：开战硬切回中心格，且无归位倒计时/离巢状态
            Assert.AreEqual(SlotId.Board(5), board.AvatarSlot.Value, "节点开始后 Avatar 必须在中心格 5");
            Assert.IsFalse(board.IsAvatarOffHome.Value, "节点开始后不应有离巢状态");
            Assert.AreEqual(0, board.AvatarHomingSteps.Value, "节点开始后计数应为 0");
        }

        [Test]
        public void HomingCountdown_DoesNotOpenCardRhythmFireWindowForAvatar()
        {
            var avatar = CreateAvatarOnBoard(SlotId.Center);
            var monster = CreateMonsterOnBoard("monster.test.swappee", SlotId.Board(1));
            var board = mArch.GetModel<BoardModel>();

            Run(new SwapAvatarWithCardAction(SlotId.Board(1)));

            var recordedEvents = new List<CoreGameEvent>();
            RunAndCollectEvents(new RotateBoardClockwiseAction(true), recordedEvents);
            RunAndCollectEvents(new RotateBoardClockwiseAction(true), recordedEvents);
            RunAndCollectEvents(new RotateBoardClockwiseAction(true), recordedEvents);

            // 验收：Avatar 已回中心
            Assert.AreEqual(SlotId.Center, board.AvatarSlot.Value);

            // 验收：归位倒计时不是卡级节奏源，不得为 Avatar 打开敌方开火窗口
            var avatarFireOpened = recordedEvents.Find(e =>
                e.Type == CoreEventType.CardRhythmFireOpened && e.CardUid == avatar.Uid);
            Assert.IsNull(avatarFireOpened, "Avatar 归位倒计时不属于卡级节奏源，不得发出 CardRhythmFireOpened 事件");
        }

        private CardInstance CreateAvatarOnBoard(SlotId slot, int hp = 30, int attack = 5)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Create("avatar.test_warrior", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, slot);
            return avatar;
        }

        private CardInstance CreateMonsterOnBoard(string defId, SlotId slot, int hp = 10, int attack = 3)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            monster.Stats.SetBase(StatId.Attack, attack);
            monster.FaceUp = true;
            mArch.GetModel<BoardModel>().PlaceCard(monster, slot);
            return monster;
        }

        private void Run(GameAction action)
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Execute(action);
        }

        private void RunAndCollectEvents(GameAction action, List<CoreGameEvent> events)
        {
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var beforeCount = pipeline.EventLog.Count;
            pipeline.Execute(action);
            for (var i = beforeCount; i < pipeline.EventLog.Count; i++)
            {
                events.Add(pipeline.EventLog[i]);
            }
        }
    }
}
