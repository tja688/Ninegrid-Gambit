using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 关卡结束残留重置回归：对战期间的临时修正（UntilNodeEnds / UntilBattleEnds /
    /// UntilEnemyChanges / Once）与膨胀的当前护甲，应在清关（节点完成）时立即重置，
    /// 而不是拖到下一局 StartNode 的大重置——非对战相位玩家卡面不得带着上局残留数值乱跑。
    /// </summary>
    public class NodeEndTransientResetTests
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

        [Test]
        public void NodeCompletion_ClearsTransientModifiers_AndResetsCurrentArmor()
        {
            var avatar = CreateAvatarOnBoard(attack: 3, armor: 2);
            StartCombatNode();

            // 制造对战残留：关卡级临时攻 buff + 战斗内膨胀的当前护甲。
            var statSystem = mArch.GetSystem<IStatSystem>();
            statSystem.AddModifier(avatar, new StatModifier(
                StatId.Attack,
                ModifierOp.Add,
                4f,
                ModifierLayer.Temporary,
                new ModifierSource("test.node_buff"),
                ModifierScope.UntilNodeEnds));
            Run(new GainArmorAction(avatar.Uid, 7, "test", "test"));

            Assert.AreEqual(7, statSystem.GetEffectiveInt(avatar, StatId.Attack), "残留前提：临时攻 buff 生效");
            Assert.AreEqual(9, StatArmorUtility.GetCurrentArmor(avatar), "残留前提：当前甲已膨胀");

            // 清关（离开机关击破）→ 节点完成。
            mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            var result = mArch.GetSystem<IPhaseSystem>().TryCompleteClearedNode();
            Assert.IsTrue(result.Accepted, "清关推进应被接受: " + result.Reason);
            Assert.AreEqual(
                GamePhase.RoomChoice,
                mArch.GetModel<RunModel>().Phase.Value,
                "清关后应进入 RoomChoice");

            Assert.AreEqual(
                3,
                statSystem.GetEffectiveInt(avatar, StatId.Attack),
                "UntilNodeEnds 临时攻 buff 应在关卡结束时清除");
            Assert.AreEqual(
                2,
                StatArmorUtility.GetCurrentArmor(avatar),
                "当前护甲应在关卡结束时回落到有效护甲");
        }

        [Test]
        public void NodeCompletion_ClearsOnceScopedNextStrikeResidue()
        {
            var avatar = CreateAvatarOnBoard(attack: 3, armor: 0);
            StartCombatNode();

            var statSystem = mArch.GetSystem<IStatSystem>();
            statSystem.RuleModifiers.Add(new RuleModifier(
                RuleId.DamageMultiplier,
                ModifierOp.Add,
                1f,
                ModifierLayer.Temporary,
                new ModifierSource("test.next_strike"),
                ModifierScope.Once,
                new TargetUidCondition(avatar.Uid)));

            mArch.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            var result = mArch.GetSystem<IPhaseSystem>().TryCompleteClearedNode();
            Assert.IsTrue(result.Accepted, "清关推进应被接受: " + result.Reason);

            var context = statSystem.CreateContext(avatar);
            Assert.AreEqual(
                0f,
                statSystem.EvaluateRule(RuleId.DamageMultiplier, 0f, context),
                "未消耗的下一击乘区残留应在关卡结束时清除");
        }

        // ==================== 基建 ====================

        private CardInstance CreateAvatarOnBoard(int attack, int armor)
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private void StartCombatNode()
        {
            var run = mArch.GetModel<RunModel>();
            run.NodeIndex.Value = 0;
            run.SetPhase(GamePhase.NodeCompleted);
            var result = mArch.GetSystem<IPhaseSystem>().StartNode(null);
            Assert.IsTrue(result.Accepted, "StartNode 应被接受: " + result.Reason);
            Assert.AreEqual(GamePhase.InteractionLoop, run.Phase.Value, "战斗节点应进入 InteractionLoop");
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }
    }
}
