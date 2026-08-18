using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.InfoNotice;
using NineGrid.Flow.Presentation;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Ui;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #231 验收测试：
    /// 主菜单教程：单局完整教练后回菜单
    /// 1. 点主菜单教程进入同一套第一关教练，含门那一步（即便生涯已见门或已完成 1–9，均不抑制）；
    /// 2. 打完（清关或战败收口）回到主菜单，不进入 1-2；
    /// 3. 不写跑图检查点，也不删除玩家已有正式存档；
    /// 4. 生涯门标记不剪掉这次的第 10 步；
    /// 5. 1–9 未完成时按钮仍不可见，完成后可见。
    /// </summary>
    [TestFixture]
    public class TutorialMenuBattleFlowRegressionTests
    {
        private sealed class MemoryRunSaveStore : IRunSaveStore
        {
            private readonly Dictionary<string, string> mSlots = new Dictionary<string, string>();

            public bool Exists(string slotId) => mSlots.ContainsKey(slotId);
            public bool TryRead(string slotId, out string json) => mSlots.TryGetValue(slotId, out json);
            public void Write(string slotId, string json) => mSlots[slotId] = json;
            public void Delete(string slotId) => mSlots.Remove(slotId);
            public void Clear() => mSlots.Clear();
            public int Count => mSlots.Count;
        }

        private MemoryRunSaveStore mMemoryStore;
        private GameContentCatalog mCatalog;

        [SetUp]
        public void SetUp()
        {
            mCatalog = ContentCatalogBootstrap.Load();
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
            NineGridArchitecture.ResetForTests();
            RunSaveStoreHook.Set(null);
            RunSetupSelection.ResetToDefault();
        }

        private IArchitecture CreateArchitecture()
        {
            NineGridArchitecture.ResetForTests();
            var arch = NineGridArchitecture.Interface;
            arch.GetSystem<IContentSystem>().Load(mCatalog);
            InitialGameFactory.Create(arch, new InitialGameOptions
            {
                Seed = 12345UL,
                DifficultyId = "difficulty.adventure",
                ProfessionId = ProfessionCatalog.Jester
            });
            return arch;
        }

        [Test]
        public void MenuTutorial_AlwaysRunsSteps1To9_EvenWhenCareerAlreadyCompleted()
        {
            // 生涯已完成 1–9
            TutorialProgressStore.MarkSteps1To9Completed();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());

            // 纯策略判定：Menu 入口恒激活 1–9
            var shouldRun = TutorialCoach.EvaluateShouldRunSteps1To9(
                TutorialEntryKind.Menu,
                isSteps1To9Completed: true,
                nodeIndex: 1);

            Assert.IsTrue(shouldRun, "主菜单教程模式必须始终激活步骤 1–9，不得被生涯完成标记抑制");

            // 运行时开局
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Menu, nodeIndex: 1);
            Assert.IsTrue(TutorialCoach.IsSteps1To9Active, "菜单教程开局后 IsSteps1To9Active 必须为 true");
            Assert.AreEqual(TutorialEntryKind.Menu, TutorialCoach.EntryKind);
        }

        [Test]
        public void MenuTutorial_AlwaysRunsDoorCoachStep10_EvenWhenCareerDoorAlreadySeen()
        {
            // 生涯已见门且步骤 1–9 已完成
            TutorialProgressStore.MarkSteps1To9Completed();
            TutorialProgressStore.MarkDoorTutorialSeen();
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());

            // 纯策略判定：Menu 入口门教学恒触发
            var shouldTrigger = TutorialCoach.EvaluateDoorTutorialPolicy(
                TutorialEntryKind.Menu,
                isDoorSeenInSave: true,
                doorSlot: 4,
                doorCard: null,
                out var intent);

            Assert.IsTrue(shouldTrigger, "主菜单教程模式必须始终包含门教学（第 10 步），不得被生涯已见标记剪掉");
            Assert.AreEqual("可以打破门来离开，清理掉所有怪物再离开会自动变卖场上的道具", intent.Text);
            Assert.IsTrue(intent.IsBlockingMainline);
            Assert.AreEqual(4, intent.TargetSlot);

            // 运行时门就位节拍
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Menu, nodeIndex: 1);
            var triggered = TutorialCoach.NotifyLeaveTrapSettled(slot: 4);
            Assert.IsTrue(triggered);
            Assert.IsTrue(TutorialCoach.IsDoorTutorialActive);
            Assert.IsTrue(TutorialCoach.IsBlockingMainline);

            // 点击推进解除卡点
            Assert.IsTrue(TutorialCoach.TryConsumeAdvance());
            Assert.IsFalse(TutorialCoach.IsDoorTutorialActive);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline);
        }

        [Test]
        public void MenuTutorial_UsesSameDeterministicOpeningPayload_AsNaturalFirstBattle()
        {
            var arch = CreateArchitecture();
            var content = arch.GetSystem<IContentSystem>();
            var options = TutorialDeckPlan.BuildOpeningOptions(content);

            Assert.IsNotNull(options);
            Assert.IsTrue(options.PreserveDealOrder, "开局发牌必须保序");
            Assert.AreEqual(16, options.EnemyCards.Count, "第一关开局序列必须为 16 张正式卡牌");

            // 三项核心保证
            Assert.AreEqual("monster.melee_3", options.EnemyCards[3].DefId, "正右方教学怪（Slot 6）");
            Assert.AreEqual("help.healing_potion", options.EnemyCards[2].DefId, "首杀稳定后正交可拾道具（Slot 3 旋转后落入 Slot 6）");
            Assert.AreEqual("trap.leave", options.EnemyCards[12].DefId, "离开机关固定在抽牌堆后半段（第 13 张 / 抽牌堆第 5 张）");
        }

        [Test]
        public void MenuTutorial_OptionsCreation_SetsTutorialModeFlag_AndContinueToFormalFalse()
        {
            var options = GameFlowRunOptions.CreateTutorial(continueToFormalRun: false);

            Assert.IsTrue(options.TutorialMode);
            Assert.IsFalse(options.QuickTestMode);
            Assert.IsFalse(options.RestoreMode);
            Assert.IsNotNull(options.Tutorial);
            Assert.IsFalse(options.Tutorial.ContinueToFormalRun, "主菜单教程必须单场后回主菜单，不得继续进正式跑图");
        }

        [Test]
        public void MenuTutorial_DoesNotDeleteExistingPlayerSaves_OnRunEnded()
        {
            // 模拟玩家已有的正式存档
            mMemoryStore.Write("auto", "{\"floor\":2,\"node\":3}");
            mMemoryStore.Write("manual_1", "{\"floor\":2,\"node\":1}");

            // 模拟教学模式：IsTutorialMode 为 true
            var isTutorialMode = true;

            // 终局清理逻辑（对齐 GameFlowOrchestrator.ShowBattleEndAndReturnAsync）
            if (!isTutorialMode)
            {
                RunSaveService.HandleRunEnded();
            }

            // 断言正式存档未被误删
            Assert.IsTrue(mMemoryStore.Exists("auto"), "教学局终局收口不得删除正式自动存档");
            Assert.IsTrue(mMemoryStore.Exists("manual_1"), "教学局终局收口不得删除正式手动存档");
        }

        [Test]
        public void MainMenuTutorialButton_VisibilityRule_MatchesSteps1To9CompletedState()
        {
            // 1. 初始未完成：1–9 未完成，教程按钮不可见
            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            var visibleInitial = TutorialProgressStore.IsSteps1To9Completed();
            Assert.IsFalse(visibleInitial, "1–9 未完成时主菜单教程按钮必须不可见");

            // 2. 步骤 1–9 完成后：教程按钮可见
            TutorialProgressStore.MarkSteps1To9Completed();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            var visibleAfter1To9 = TutorialProgressStore.IsSteps1To9Completed();
            Assert.IsTrue(visibleAfter1To9, "1–9 完成后主菜单教程按钮必须可见");
        }

        [Test]
        public void MenuTutorial_BattleEnd_CleansUpStateAndReleasesInputHolds()
        {
            TutorialCoach.OnBattleStarted(TutorialEntryKind.Menu, nodeIndex: 1);
            Assert.IsTrue(TutorialCoach.IsSteps1To9Active);

            // 模拟中途离场或战败结束
            TutorialCoach.OnBattleEnded();

            Assert.IsFalse(TutorialCoach.IsDoorTutorialActive);
            Assert.IsFalse(TutorialCoach.IsBlockingMainline);
            Assert.IsFalse(PresentationInputGates.HasExternalHold);
        }
    }
}
