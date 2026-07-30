using System;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// #43 批次6：GameFlow Shell 只读权威与 Command / Signal。
    /// </summary>
    public sealed class GameFlowShellAuthorityTests
    {
        [Test]
        public void Shell_ExposesReadOnlyAuthority_Defaults()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                Assert.AreEqual(GameFlowShellState.MainMenu, shell.State.Value);
                Assert.AreEqual(0, shell.NodeIndex);
                Assert.AreEqual(0, shell.Generation);
                Assert.IsFalse(shell.IsBusy);
                Assert.IsFalse(shell.IsTestMode);
                Assert.IsFalse(shell.IsQuickTestMode);
                Assert.IsTrue(shell.CanAcceptQuickTestEntry);
            }
        }

        [Test]
        public void BeginRunCommand_WritesRunMode_AndBumpsGeneration()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                shell.Bind(new FakeGameFlowView());

                LogAssert.Expect(LogType.Error, "[GameFlow] 未绑定 IBattleSessionSystem，无法入场。");
                LogAssert.Expect(LogType.Error, "[GameFlow] 局内会话未就绪，终止节点循环。");

                NineGridArchitecture.Interface.SendCommand(
                    new BeginGameFlowRunCommand(testMode: true, quickTestMode: false));

                Assert.IsTrue(shell.IsTestMode);
                Assert.IsFalse(shell.IsQuickTestMode);
                Assert.AreEqual(1, shell.Generation);
                Assert.AreEqual(GameFlowShellState.BattleStub, shell.State.Value);

                NineGridArchitecture.Interface.SendCommand(new ReturnToMainMenuCommand());
                Assert.AreEqual(GameFlowShellState.MainMenu, shell.State.Value);
                Assert.IsFalse(shell.IsTestMode);
                Assert.AreEqual(2, shell.Generation);
            }
        }

        [Test]
        public void SignalBattleEnded_AdvancesToVictoryNotice()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                shell.Bind(new FakeGameFlowView());
                NineGridArchitecture.Interface.SendCommand(
                    new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));

                NineGridArchitecture.Interface.SendCommand(
                    SignalGameFlowCommand.BattleEnded(victory: true));

                Assert.AreEqual(GameFlowShellState.VictoryNotice, shell.State.Value);
            }
        }

        [Test]
        public void DisableDomainReloadResidue_BlocksQuickTestUntilForceMainMenu()
        {
            // 模拟 Editor DisableDomainReload：Play 退出后 Shell 仍 Busy + 非 MainMenu，
            // 下一局 Awake 若不复位，StartRun / 快速测试入口会被门禁静默吞掉。
            using (PresentationArchitectureFixture.CreateBare())
            {
                var shell = GameFlowShellSystem.EnsureRegistered();
                shell.Bind(new FakeGameFlowView());
                NineGridArchitecture.Interface.SendCommand(
                    new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));
                typeof(GameFlowShellSystem)
                    .GetMethod("SetBusy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(shell, new object[] { true });

                Assert.IsFalse(shell.CanAcceptQuickTestEntry);
                var genBefore = shell.Generation;
                LogAssert.Expect(LogType.Warning, "[GameFlow] 当前循环仍在进行，忽略 BeginRun。");
                shell.BeginRun(new GameFlowRunOptions { TestMode = true });
                Assert.AreEqual(genBefore, shell.Generation, "残留 Busy 时应忽略 BeginRun");
                Assert.AreEqual(GameFlowShellState.BattleStub, shell.State.Value);

                shell.ForceMainMenuAuthority();
                Assert.AreEqual(GameFlowShellState.MainMenu, shell.State.Value);
                Assert.IsFalse(shell.IsBusy);
                Assert.IsTrue(shell.CanAcceptQuickTestEntry);
            }
        }

        [Test]
        public void SignalSettlementReady_DoesNotThrow_WhenNoActiveWait()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                GameFlowShellSystem.EnsureRegistered().Bind(new FakeGameFlowView());
                Assert.DoesNotThrow(() =>
                    NineGridArchitecture.Interface.SendCommand(
                        SignalGameFlowCommand.SettlementReady()));
            }
        }

        [Test]
        public void Interface_IsReadOnly_WriteApiOnConcreteOnly()
        {
            Assert.IsNull(typeof(IGameFlowShellSystem).GetMethod("SetState"));
            Assert.IsNull(typeof(IGameFlowShellSystem).GetMethod("ApplyState"));
            Assert.IsNotNull(typeof(GameFlowShellSystem).GetMethod("ApplyState"));
            Assert.IsNotNull(typeof(GameFlowShellSystem).GetMethod("BeginRun"));
            Assert.IsNotNull(typeof(GameFlowShellSystem).GetMethod("Signal"));
        }

        private sealed class FakeGameFlowView : IGameFlowView
        {
            public float RoomEventStubSeconds => 0.05f;
            public float VictoryNoticeSeconds => 0.05f;
            public float DefeatNoticeSeconds => 0.05f;
            public string VictoryMessage => "胜利";
            public string DefeatMessage => "失败";
            public bool IsRoomChoiceActive => false;

            public void EnsureViewBindings()
            {
            }

            public void ShowNotice(string message)
            {
            }

            public void HideNotice()
            {
            }

            public void ShowMainMenuPanels()
            {
            }

            public void ShowInRunShell(bool inBattle = true)
            {
            }

            public void ShowRewardOverlay()
            {
            }

            public void ShowRoomChoiceOverlay()
            {
            }

            public void ShowRoomEventOverlay()
            {
            }

            public void HideAllOverlays()
            {
            }

            public void BeginRoomChoice(
                string leftLabel,
                string rightLabel,
                Action<int, string> onPicked,
                Action onFinished,
                bool hoverOnNotice)
            {
                onFinished?.Invoke();
            }

            public void HideRoomChoice()
            {
            }

            public void QuitGame()
            {
            }
        }
    }
}
