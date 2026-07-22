using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.FlowShell
{
    /// <summary>
    /// V8：流程壳相位 Command → System BindableProperty + 离开战斗 HardClear Event。
    /// </summary>
    public sealed class SetGameFlowShellStateCommandTests
    {
        [Test]
        public void Command_UpdatesShellState_AndEmitsChangedEvent()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var architecture = NineGridArchitecture.Interface;
                architecture.RegisterSystem(new GameFlowShellSystem());

                GameFlowShellStateChangedEvent? changed = null;
                var unreg = architecture.RegisterEvent<GameFlowShellStateChangedEvent>(e => changed = e);
                try
                {
                    architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));

                    var shell = architecture.GetSystem<IGameFlowShellSystem>();
                    Assert.AreEqual(GameFlowShellState.BattleStub, shell.State.Value);
                    Assert.IsTrue(changed.HasValue);
                    Assert.AreEqual(GameFlowShellState.MainMenu, changed.Value.From);
                    Assert.AreEqual(GameFlowShellState.BattleStub, changed.Value.To);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void Command_EnteringMainMenu_RequestsDirectorTeardown()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var architecture = NineGridArchitecture.Interface;
                architecture.RegisterSystem(new GameFlowShellSystem());
                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));

                TeardownPresentationDirectorRequested? teardown = null;
                var unreg = architecture.RegisterEvent<TeardownPresentationDirectorRequested>(
                    e => teardown = e);
                try
                {
                    architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.MainMenu));

                    Assert.IsTrue(teardown.HasValue);
                    Assert.AreEqual(IntentClearReason.LayerChange, teardown.Value.Reason);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void Command_EnteringVictoryNotice_RequestsPhaseChangeHardClear()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var architecture = NineGridArchitecture.Interface;
                architecture.RegisterSystem(new GameFlowShellSystem());
                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));

                TeardownPresentationDirectorRequested? teardown = null;
                var unreg = architecture.RegisterEvent<TeardownPresentationDirectorRequested>(
                    e => teardown = e);
                try
                {
                    architecture.SendCommand(
                        new SetGameFlowShellStateCommand(GameFlowShellState.VictoryNotice));

                    Assert.IsTrue(teardown.HasValue);
                    Assert.AreEqual(IntentClearReason.PhaseChange, teardown.Value.Reason);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void Command_RewardOrRoomOverlay_DoesNotRequestTeardown()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var architecture = NineGridArchitecture.Interface;
                architecture.RegisterSystem(new GameFlowShellSystem());
                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));

                var teardownCount = 0;
                var unreg = architecture.RegisterEvent<TeardownPresentationDirectorRequested>(
                    _ => teardownCount++);
                try
                {
                    architecture.SendCommand(
                        new SetGameFlowShellStateCommand(GameFlowShellState.RewardChoice));
                    architecture.SendCommand(
                        new SetGameFlowShellStateCommand(GameFlowShellState.RoomChoice));
                    architecture.SendCommand(
                        new SetGameFlowShellStateCommand(GameFlowShellState.RoomEvent));

                    Assert.AreEqual(0, teardownCount);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }
    }
}
