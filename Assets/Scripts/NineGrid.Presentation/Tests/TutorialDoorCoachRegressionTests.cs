using System.Collections.Generic;
using NineGrid.Flow;
using NineGrid.Flow.InfoNotice;
using NineGrid.Flow.Tutorial;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #229 验收测试：
    /// 独立门教学（生涯首次就位提示、指定文案、卡住主流程、点击推进写已见、菜单教程不抑制、独立于步骤 1–9）。
    /// </summary>
    [TestFixture]
    public class TutorialDoorCoachRegressionTests
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
        public void PurePolicy_NaturalRun_WhenDoorNotSeen_TriggersStep10()
        {
            var triggered = TutorialCoach.EvaluateDoorTutorialPolicy(
                TutorialEntryKind.Natural,
                isDoorSeenInSave: false,
                doorSlot: 3,
                doorCard: null,
                out var intent);

            Assert.IsTrue(triggered);
            Assert.AreEqual("可以打破门来离开，清理掉所有怪物再离开会自动变卖场上的道具", intent.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, intent.HoldMode);
            Assert.IsTrue(intent.IsBlockingMainline);
            Assert.AreEqual(3, intent.TargetSlot);
        }

        [Test]
        public void PurePolicy_NaturalRun_WhenDoorAlreadySeen_DoesNotTrigger()
        {
            var triggered = TutorialCoach.EvaluateDoorTutorialPolicy(
                TutorialEntryKind.Natural,
                isDoorSeenInSave: true,
                doorSlot: 3,
                doorCard: null,
                out var intent);

            Assert.IsFalse(triggered);
            Assert.IsFalse(intent.IsActive);
        }

        [Test]
        public void PurePolicy_MenuRun_WhenDoorAlreadySeen_StillTriggersStep10()
        {
            // 主菜单教程不走生涯抑制，每次都完整包含门教学这一步
            var triggered = TutorialCoach.EvaluateDoorTutorialPolicy(
                TutorialEntryKind.Menu,
                isDoorSeenInSave: true,
                doorSlot: 6,
                doorCard: null,
                out var intent);

            Assert.IsTrue(triggered);
            Assert.AreEqual("可以打破门来离开，清理掉所有怪物再离开会自动变卖场上的道具", intent.Text);
            Assert.AreEqual(InfoNoticeHoldMode.ClickToAdvance, intent.HoldMode);
            Assert.IsTrue(intent.IsBlockingMainline);
            Assert.AreEqual(6, intent.TargetSlot);
        }

        [Test]
        public void NaturalRun_FirstDoorSettled_TriggersStep10AndBlocks_ClickAdvancesAndWritesSeen()
        {
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural);
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialCoach.IsDoorTutorialActive);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline);

            // 1. 离开机关就位
            var triggered = TutorialCoach.NotifyLeaveTrapSettled(slot: 2);
            Assert.IsTrue(triggered);
            Assert.IsTrue(TutorialCoach.IsDoorTutorialActive);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);
            Assert.IsTrue(InfoNoticePresenter.IsHoldingSentence);

            // 2. 玩家主键点击推进（推进前未写已见）
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());

            var consumed = TutorialCoach.TryConsumeAdvance();
            Assert.IsTrue(consumed);

            // 3. 提示结束即写门已见，主流程解除卡点
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialCoach.IsDoorTutorialActive);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline);
            Assert.IsFalse(InfoNoticePresenter.IsHoldingSentence);
        }

        [Test]
        public void NaturalRun_SubsequentRuns_DoNotTriggerDoorTutorialAgain()
        {
            // 生涯已见
            TutorialProgressStore.MarkDoorTutorialSeen();
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());

            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural);

            var triggered = TutorialCoach.NotifyLeaveTrapSettled(slot: 8);
            Assert.IsFalse(triggered);
            Assert.IsFalse(TutorialCoach.IsDoorTutorialActive);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline);
        }

        [Test]
        public void NaturalRun_DefeatBeforeDoor_PreservesUnseen_NextRunTriggersDoorTutorial()
        {
            // 第一局：步骤 1–9 或战败收口，但没有见到门就位
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural);
            TutorialProgressStore.MarkSteps1To9Completed(); // 战败写 1–9
            TutorialCoach.OnBattleEnded();

            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen()); // 门仍未见

            // 第二局：首次见到门就位
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural);
            var triggered = TutorialCoach.NotifyLeaveTrapSettled(slot: 7);

            Assert.IsTrue(triggered);
            Assert.IsTrue(TutorialCoach.IsDoorTutorialActive);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);

            // 推进后写入
            TutorialCoach.TryConsumeAdvance();
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
        }

        [Test]
        public void MenuTutorial_AlwaysTriggersDoorTutorial_RegardlessOfCareerFlag()
        {
            // 生涯已见门且步骤 1–9 已完成
            TutorialProgressStore.MarkSteps1To9Completed();
            TutorialProgressStore.MarkDoorTutorialSeen();
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());

            TutorialCoach.OnBattleStarted(TutorialEntryKind.Menu);

            var triggered = TutorialCoach.NotifyLeaveTrapSettled(slot: 4);
            Assert.IsTrue(triggered, "菜单教程模式每次均完整包含门教学");
            Assert.IsTrue(TutorialCoach.IsDoorTutorialActive);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);

            var consumed = TutorialCoach.TryConsumeAdvance();
            Assert.IsTrue(consumed);
            Assert.IsFalse(TutorialCoach.IsDoorTutorialActive);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline);
        }

        [Test]
        public void DoorTutorial_IsIndependentFromSteps1To9()
        {
            // 步骤 1–9 未完成时，门就位可触发
            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Natural);
            Assert.IsTrue(TutorialCoach.NotifyLeaveTrapSettled(slot: 1));
            TutorialCoach.TryConsumeAdvance();
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed()); // 不影响 1–9 独立标记
        }
    }
}
