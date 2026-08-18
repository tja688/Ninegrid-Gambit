using System.Collections.Generic;
using NineGrid.Flow;
using NineGrid.Flow.InfoNotice;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #230 验收测试：
    /// 自然入口第一关：步骤 1–9 教练
    /// 1. 纯策略断言：步骤 1–9 激活条件、各步文案、保持方式、主流程阻断、白名单格位、右键详述与规则书放行规则；
    /// 2. 状态机节拍链路：Avatar就位(1/2) → 发牌就位(3) → 邻格攻击(4) → 击杀补牌旋转(5) → 道具拾取(6) → 行动/右键/规则书定时(7/8/9) → 标记写入与放手；
    /// 3. 错点其它格被收口拒绝，不另刷报错弹窗；
    /// 4. 战败亦写 1–9 标记，后续自然对局不再出 1–9；
    /// 5. 清关进入 1-2 节点，后续对局不再叠 1–9。
    /// </summary>
    [TestFixture]
    public class TutorialCoachSteps1To9RegressionTests
    {
        private sealed class MemoryRunSaveStore : IRunSaveStore
        {
            private readonly Dictionary<string, string> mSlots = new Dictionary<string, string>();

            public bool Exists(string slotId) => mSlots.ContainsKey(slotId);
            public bool TryRead(string slotId, out string json) => mSlots.TryGetValue(slotId, out json);
            public void Write(string slotId, string json) => mSlots[slotId] = json;
            public void Delete(string slotId) => mSlots.Remove(slotId);
            public void Clear() => mSlots.Clear();
        }

        private MemoryRunSaveStore mMemoryStore;

        [SetUp]
        public void SetUp()
        {
            mMemoryStore = new MemoryRunSaveStore();
            RunSaveStoreHook.Set(mMemoryStore);
            TutorialProgressStore.ResetAll();
            TutorialCoach.ResetForTests();
            InfoNoticePresenter.ResetForTests();
            TutorialPromptBoxPresenter.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            TutorialCoach.ResetForTests();
            InfoNoticePresenter.ResetForTests();
            TutorialPromptBoxPresenter.ResetForTests();
            RunSaveStoreHook.Set(null);
        }

        [Test]
        public void PurePolicy_Activation_NaturalFirstBattle_ShouldRun()
        {
            var shouldRun = TutorialCoach.EvaluateShouldRunSteps1To9(
                TutorialEntryKind.Natural,
                isSteps1To9Completed: false,
                nodeIndex: 1);

            Assert.IsTrue(shouldRun, "自然对局首关且未完成 1–9 时应激活步骤 1–9");
        }

        [Test]
        public void PurePolicy_Activation_NaturalAfterCompleted_ShouldNotRun()
        {
            var shouldRun = TutorialCoach.EvaluateShouldRunSteps1To9(
                TutorialEntryKind.Natural,
                isSteps1To9Completed: true,
                nodeIndex: 1);

            Assert.IsFalse(shouldRun, "自然对局 1–9 完成后不应再次激活");
        }

        [Test]
        public void PurePolicy_Activation_NaturalNode2_ShouldNotRun()
        {
            var shouldRun = TutorialCoach.EvaluateShouldRunSteps1To9(
                TutorialEntryKind.Natural,
                isSteps1To9Completed: false,
                nodeIndex: 2);

            Assert.IsFalse(shouldRun, "自然对局 1-2 (节点2) 不得激活步骤 1–9");
        }

        [Test]
        public void PurePolicy_Activation_MenuTutorial_AlwaysRuns()
        {
            var shouldRun1 = TutorialCoach.EvaluateShouldRunSteps1To9(
                TutorialEntryKind.Menu,
                isSteps1To9Completed: false,
                nodeIndex: 1);
            var shouldRun2 = TutorialCoach.EvaluateShouldRunSteps1To9(
                TutorialEntryKind.Menu,
                isSteps1To9Completed: true,
                nodeIndex: 1);

            Assert.IsTrue(shouldRun1, "菜单教程模式未完成时应激活 1–9");
            Assert.IsTrue(shouldRun2, "菜单教程模式已完成时亦应激活 1–9（复习教学）");
        }

        [Test]
        public void PurePolicy_StepIntents_MatchSpecification()
        {
            // 步骤 1: 欢迎
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step1_Welcome, 5, null, out var i1));
            Assert.AreEqual("欢迎来到九宫地下城，在开始之前，让我们了解一些基础操作", i1.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, i1.HoldMode);
            Assert.AreEqual(5, i1.TargetSlot);
            Assert.IsTrue(i1.IsBlockingMainline);

            // 步骤 2: 顺劈斧
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step2_WarriorCleave, 5, null, out var i2));
            Assert.AreEqual("这是你的初始角色，战士可以使用自带的顺劈斧进行范围攻击", i2.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, i2.HoldMode);
            Assert.AreEqual(5, i2.TargetSlot);
            Assert.IsTrue(i2.IsBlockingMainline);

            // 步骤 3: 铺满8张
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step3_Opening8Cards, -1, null, out var i3));
            Assert.AreEqual("每次开局都会发8张牌铺满场地", i3.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, i3.HoldMode);
            Assert.IsTrue(i3.IsBlockingMainline);

            // 步骤 4: 攻击邻格怪
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step4_AttackMonster, 2, null, out var i4));
            Assert.AreEqual("点击你交互范围内的敌方卡牌可以进行攻击并准备迎接敌方的反击", i4.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, i4.HoldMode);
            Assert.AreEqual(2, i4.TargetSlot);
            Assert.IsFalse(i4.IsBlockingMainline, "步骤 4 不阻断主流程，允许玩家点击目标攻击");

            // 步骤 5: 击杀补牌旋转
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step5_RefillAndRotate, -1, null, out var i5));
            Assert.AreEqual("击杀后若场地缺牌会从卡组补充，并触发一次旋转", i5.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, i5.HoldMode);
            Assert.IsTrue(i5.IsBlockingMainline);

            // 步骤 6: 拾取道具
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step6_PickupPotion, 2, null, out var i6));
            Assert.AreEqual("可以拾取道具卡，放入手牌并随时使用，手牌可以跨战斗保存", i6.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, i6.HoldMode);
            Assert.AreEqual(2, i6.TargetSlot);
            Assert.IsFalse(i6.IsBlockingMainline, "步骤 6 不阻断主流程，允许玩家拾取道具");

            // 步骤 7: 行动计数
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step7_MonsterCountdowns, -1, null, out var i7));
            Assert.AreEqual("当心，待怪物行动计数归零后他们会主动发起效果或攻击", i7.Text);
            Assert.AreEqual(InfoNoticeHoldMode.HoldTwoSeconds, i7.HoldMode);
            Assert.IsTrue(i7.IsBlockingMainline);

            // 步骤 8: 右键详述
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step8_InspectCards, -1, null, out var i8));
            Assert.AreEqual("可以右键点击场地卡牌、手牌或者遗物来查看它们的详细描述", i8.Text);
            Assert.AreEqual(InfoNoticeHoldMode.HoldTwoSeconds, i8.HoldMode);
            Assert.IsTrue(i8.IsBlockingMainline);

            // 步骤 9: 规则书
            Assert.IsTrue(TutorialCoach.EvaluateStepIntent(TutorialCoachStep.Step9_RuleBook, -1, null, out var i9));
            Assert.AreEqual("你可以点击规则书了解更多内容，祝你游戏愉快！", i9.Text);
            Assert.AreEqual(InfoNoticeHoldMode.HoldTwoSeconds, i9.HoldMode);
            Assert.IsTrue(i9.IsBlockingMainline);
        }

        [Test]
        public void PurePolicy_RightClickInspect_AllowedFromStep8()
        {
            // 步骤 1–7 禁用
            for (var step = TutorialCoachStep.Step1_Welcome; step <= TutorialCoachStep.Step7_MonsterCountdowns; step++)
            {
                Assert.IsFalse(
                    TutorialCoach.EvaluateRightClickInspectAllowed(step, isSteps1To9Active: true),
                    $"步骤 {step} 必须禁用右键详述");
            }

            // 步骤 8、9、10、Completed 启用
            Assert.IsTrue(TutorialCoach.EvaluateRightClickInspectAllowed(TutorialCoachStep.Step8_InspectCards, isSteps1To9Active: true));
            Assert.IsTrue(TutorialCoach.EvaluateRightClickInspectAllowed(TutorialCoachStep.Step9_RuleBook, isSteps1To9Active: true));
            Assert.IsTrue(TutorialCoach.EvaluateRightClickInspectAllowed(TutorialCoachStep.Step10_LeaveDoor, isSteps1To9Active: true));
            Assert.IsTrue(TutorialCoach.EvaluateRightClickInspectAllowed(TutorialCoachStep.Completed, isSteps1To9Active: true));

            // 非 1–9 教学期间恒启用
            Assert.IsTrue(TutorialCoach.EvaluateRightClickInspectAllowed(TutorialCoachStep.None, isSteps1To9Active: false));
        }

        [Test]
        public void PurePolicy_Rulebook_AllowedFromStep9()
        {
            // 步骤 1–8 禁用
            for (var step = TutorialCoachStep.Step1_Welcome; step <= TutorialCoachStep.Step8_InspectCards; step++)
            {
                Assert.IsFalse(
                    TutorialCoach.EvaluateRulebookAllowed(step, isSteps1To9Active: true),
                    $"步骤 {step} 必须禁用规则书");
            }

            // 步骤 9、10、Completed 启用
            Assert.IsTrue(TutorialCoach.EvaluateRulebookAllowed(TutorialCoachStep.Step9_RuleBook, isSteps1To9Active: true));
            Assert.IsTrue(TutorialCoach.EvaluateRulebookAllowed(TutorialCoachStep.Step10_LeaveDoor, isSteps1To9Active: true));
            Assert.IsTrue(TutorialCoach.EvaluateRulebookAllowed(TutorialCoachStep.Completed, isSteps1To9Active: true));

            // 非 1–9 教学期间恒启用
            Assert.IsTrue(TutorialCoach.EvaluateRulebookAllowed(TutorialCoachStep.None, isSteps1To9Active: false));
        }

        [Test]
        public void PurePolicy_IntentLegality_Step4WhitelistsSlot2AttackOnly()
        {
            var attackSlot2 = new InputIntent(InputIntentKinds.Attack, 2);
            var attackSlot3 = new InputIntent(InputIntentKinds.Attack, 3);
            var exploreSlot1 = new InputIntent(InputIntentKinds.Explore, 1);
            var pickupSlot2 = new InputIntent(InputIntentKinds.Pickup, 2);

            Assert.IsTrue(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step4_AttackMonster, true, attackSlot2, out _),
                "步骤 4 必须放行 Slot 2 的攻击意图");

            Assert.IsFalse(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step4_AttackMonster, true, attackSlot3, out var r1),
                "步骤 4 必须拒绝 Slot 3 的攻击");
            Assert.AreEqual("tutorialRestrictedTarget", r1);

            Assert.IsFalse(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step4_AttackMonster, true, exploreSlot1, out _),
                "步骤 4 必须拒绝探索格位");

            Assert.IsFalse(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step4_AttackMonster, true, pickupSlot2, out _),
                "步骤 4 必须拒绝非攻击意图");
        }

        [Test]
        public void PurePolicy_IntentLegality_Step6WhitelistsSlot2PickupOnly()
        {
            var pickupSlot2 = new InputIntent(InputIntentKinds.Pickup, 2);
            var pickupSlot1 = new InputIntent(InputIntentKinds.Pickup, 1);
            var attackSlot2 = new InputIntent(InputIntentKinds.Attack, 2);

            Assert.IsTrue(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step6_PickupPotion, true, pickupSlot2, out _),
                "步骤 6 必须放行 Slot 2 的拾取意图");

            Assert.IsFalse(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step6_PickupPotion, true, pickupSlot1, out var r1),
                "步骤 6 必须拒绝 Slot 1 的拾取");
            Assert.AreEqual("tutorialRestrictedTarget", r1);

            Assert.IsFalse(
                TutorialCoach.EvaluateIntentLegality(TutorialCoachStep.Step6_PickupPotion, true, attackSlot2, out _),
                "步骤 6 必须拒绝攻击意图");
        }

        [Test]
        public void NaturalFirstBattle_StepByStepProgression_AndWritesCompletion()
        {
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural, nodeIndex: 1);
            Assert.IsTrue(TutorialCoach.IsSteps1To9Active);
            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());

            // 1. Avatar 就位（步骤 1）
            var avatarTask = TutorialCoach.NotifyAvatarSettledAsync(avatarCard: null, System.Threading.CancellationToken.None);
            Assert.AreEqual(TutorialCoachStep.Step1_Welcome, TutorialCoach.CurrentStep);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);
            Assert.IsFalse(TutorialCoach.IsRightClickInspectAllowed);
            Assert.IsFalse(TutorialCoach.IsRulebookAllowed);

            // 2. 点击推进至步骤 2
            Assert.IsTrue(TutorialCoach.TryConsumeAdvance());
            Assert.AreEqual(TutorialCoachStep.Step2_WarriorCleave, TutorialCoach.CurrentStep);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);

            // 3. 点击推进完成 Avatar 阶段，放行发牌
            Assert.IsTrue(TutorialCoach.TryConsumeAdvance());
            Assert.AreEqual(Cysharp.Threading.Tasks.UniTaskStatus.Succeeded, avatarTask.Status);

            // 4. 开局 8 张牌发牌就位（步骤 3）
            var boardTask = TutorialCoach.NotifyOpeningBoardDealtAsync(System.Threading.CancellationToken.None);
            Assert.AreEqual(TutorialCoachStep.Step3_Opening8Cards, TutorialCoach.CurrentStep);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);

            // 5. 点击推进至步骤 4（框怪、放行 Slot 2 攻击）
            Assert.IsTrue(TutorialCoach.TryConsumeAdvance());
            Assert.AreEqual(TutorialCoachStep.Step4_AttackMonster, TutorialCoach.CurrentStep);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline, "步骤 4 主流程放行");
            Assert.AreEqual(Cysharp.Threading.Tasks.UniTaskStatus.Succeeded, boardTask.Status);

            // 6. 验证白名单过滤
            Assert.IsTrue(TutorialCoach.IsIntentAllowed(new InputIntent(InputIntentKinds.Attack, 2), out _));
            Assert.IsFalse(TutorialCoach.IsIntentAllowed(new InputIntent(InputIntentKinds.Attack, 8), out _));

            // 7. 击杀 Slot 2 怪物后，补牌与旋转完成就位（步骤 5）
            TutorialCoach.NotifyPostKillBoardSettled();
            Assert.AreEqual(TutorialCoachStep.Step5_RefillAndRotate, TutorialCoach.CurrentStep);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);

            // 8. 点击推进至步骤 6（框药水、放行 Slot 2 拾取）
            Assert.IsTrue(TutorialCoach.TryConsumeAdvance());
            Assert.AreEqual(TutorialCoachStep.Step6_PickupPotion, TutorialCoach.CurrentStep);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline, "步骤 6 主流程放行");
            Assert.IsTrue(TutorialCoach.IsIntentAllowed(new InputIntent(InputIntentKinds.Pickup, 2), out _));
            Assert.IsFalse(TutorialCoach.IsIntentAllowed(new InputIntent(InputIntentKinds.Pickup, 1), out _));

            // 9. 道具拾取完成（步骤 7）
            TutorialCoach.NotifyItemPickedUp(slot: 2);
            // 立即进入步骤 7
            Assert.AreEqual(TutorialCoachStep.Step7_MonsterCountdowns, TutorialCoach.CurrentStep);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);
        }

        [Test]
        public void NaturalBattle_OnDefeat_WritesSteps1To9Completed()
        {
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural, nodeIndex: 1);
            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());

            // 战败收口写 1–9 标记
            TutorialProgressStore.MarkSteps1To9Completed();
            TutorialCoach.OnBattleEnded();

            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed(), "首关战败必须写下 1–9 步骤完成标记");

            // 下一场自然对局不再出 1–9
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural, nodeIndex: 1);
            Assert.IsFalse(TutorialCoach.IsSteps1To9Active, "1–9 完成后后续自然对局不再激活教练");
        }
    }
}
