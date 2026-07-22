using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #43 批次0：流程壳状态顺序基线（MainMenu → Battle → Reward/Room → Victory/Defeat）。
    /// </summary>
    public sealed class FlowStateOrderBaselineTests
    {
        [Test]
        public void Shell_AllowsMainlineProgression_AndTeardownOnlyOnExitLifecycle()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var architecture = NineGridArchitecture.Interface;
                architecture.RegisterSystem<IGameFlowShellSystem>(new GameFlowShellSystem());
                var shell = architecture.GetSystem<IGameFlowShellSystem>();
                Assert.IsNotNull(shell);
                Assert.AreEqual(GameFlowShellState.MainMenu, shell.State.Value);

                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));
                Assert.AreEqual(GameFlowShellState.BattleStub, shell.State.Value);

                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.RewardChoice));
                Assert.AreEqual(GameFlowShellState.RewardChoice, shell.State.Value);

                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.RoomChoice));
                Assert.AreEqual(GameFlowShellState.RoomChoice, shell.State.Value);

                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.RoomEvent));
                Assert.AreEqual(GameFlowShellState.RoomEvent, shell.State.Value);

                var teardownCount = 0;
                IntentClearReason? lastReason = null;
                var unreg = architecture.RegisterEvent<TeardownPresentationDirectorRequested>(e =>
                {
                    teardownCount++;
                    lastReason = e.Reason;
                });
                try
                {
                    architecture.SendCommand(
                        new SetGameFlowShellStateCommand(GameFlowShellState.VictoryNotice));
                    Assert.AreEqual(GameFlowShellState.VictoryNotice, shell.State.Value);
                    Assert.AreEqual(1, teardownCount);
                    Assert.AreEqual(IntentClearReason.PhaseChange, lastReason);

                    architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.MainMenu));
                    Assert.AreEqual(GameFlowShellState.MainMenu, shell.State.Value);
                    Assert.AreEqual(2, teardownCount);
                    Assert.AreEqual(IntentClearReason.LayerChange, lastReason);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }
    }
}
