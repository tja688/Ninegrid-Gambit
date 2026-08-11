using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// ADR-0039 补遗回归：Avatar 判死谓词统一（合法指令裁决 ≡ 战败收束）。
    /// 锁死的 bug 形态：Avatar zone 被异常置死（Removed/Graveyard）但 HP&gt;0 时，
    /// InteractionLoop 合法指令被锁进「仅回收/丢弃」僵尸集（点不了敌人、道具释放被拒、只能卖卡），
    /// 而 DefeatIfAvatarDead 只认 HP 永不收束——战场永久软锁。
    /// </summary>
    public class AvatarVitalityInvariantTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        private CardInstance CreateAvatarOnBoard(int hp)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var board = mArch.GetModel<BoardModel>();
            var avatar = registry.Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            board.SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        [Test]
        public void ZoneDeadButAliveAvatar_BattlefieldCommandsStayLegal()
        {
            var avatar = CreateAvatarOnBoard(10);
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);

            // 非法状态注入：zone 置死但 HP>0（历史上会永久锁死战场）。
            avatar.Zone.Value = ZoneId.Removed;

            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsTrue(phase.CanExecute(GameCommandKind.Attack), "zone 异常但 HP>0 不得禁攻击");
            Assert.IsTrue(phase.CanExecute(GameCommandKind.UseItem), "zone 异常但 HP>0 不得禁用牌");
            Assert.IsTrue(phase.CanExecute(GameCommandKind.PickupItem), "zone 异常但 HP>0 不得禁拾取");
            Assert.IsTrue(phase.CanExecute(GameCommandKind.ClickEmpty), "zone 异常但 HP>0 不得禁点空格");
        }

        [Test]
        public void HpZeroAvatar_LegalCommandsShrinkToSlotManagement()
        {
            var avatar = CreateAvatarOnBoard(10);
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            avatar.Stats.SetBase(StatId.Hp, 0);

            var phase = mArch.GetSystem<IPhaseSystem>();
            Assert.IsFalse(phase.CanExecute(GameCommandKind.Attack), "0 血僵尸局不得攻击（ADR-0039）");
            Assert.IsFalse(phase.CanExecute(GameCommandKind.UseItem), "0 血僵尸局不得用牌（ADR-0039）");
            Assert.IsTrue(phase.CanExecute(GameCommandKind.RecycleItemSlot), "0 血仍可回收腾位");
            Assert.IsTrue(phase.CanExecute(GameCommandKind.DiscardRelic), "0 血仍可丢弃遗物");
        }

        [Test]
        public void DefeatAction_DefeatsWhenAvatarUnregistered()
        {
            var avatar = CreateAvatarOnBoard(10);
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);

            // 非法状态注入：Avatar 从注册表消失（旧实现静默 no-op → 永久僵尸）。
            mArch.GetModel<CardRegistry>().Remove(avatar.Uid);

            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new DefeatIfAvatarDeadAction());
            pipeline.RunToCompletion();

            Assert.AreEqual(
                GamePhase.Defeat,
                mArch.GetModel<RunModel>().Phase.Value,
                "Avatar 未注册时 DefeatIfAvatarDead 必须收束为 Defeat，不得留下僵尸战场");
        }

        [Test]
        public void DefeatAction_DefeatsOnZeroHp()
        {
            var avatar = CreateAvatarOnBoard(10);
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
            avatar.Stats.SetBase(StatId.Hp, 0);

            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new DefeatIfAvatarDeadAction());
            pipeline.RunToCompletion();

            Assert.AreEqual(GamePhase.Defeat, mArch.GetModel<RunModel>().Phase.Value);
        }

        [Test]
        public void StartNode_RepairsCorruptedAvatarZone()
        {
            var avatar = CreateAvatarOnBoard(10);
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 1;
            run.SetPhase(GamePhase.NodeCompleted);

            avatar.Zone.Value = ZoneId.Removed;

            var result = mArch.GetSystem<IPhaseSystem>().StartNode(null);

            Assert.IsTrue(result.Accepted, "StartNode 应被接受");
            Assert.AreEqual(
                ZoneId.Avatar,
                avatar.Zone.Value,
                "StartNode 必须把 zone 被异常置死的存活 Avatar 归位自愈");
            Assert.AreEqual(GamePhase.InteractionLoop, run.Phase.Value, "存活 Avatar 不得被误判战败");
        }

        [Test]
        public void StartNode_DefeatsZeroHpAvatar()
        {
            var avatar = CreateAvatarOnBoard(10);
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 1;
            run.SetPhase(GamePhase.NodeCompleted);
            avatar.Stats.SetBase(StatId.Hp, 0);

            mArch.GetSystem<IPhaseSystem>().StartNode(null);

            Assert.AreEqual(
                GamePhase.Defeat,
                run.Phase.Value,
                "StartNode 末尾 HP≤0 必须补跑 Defeat（ADR-0039）");
        }
    }
}
