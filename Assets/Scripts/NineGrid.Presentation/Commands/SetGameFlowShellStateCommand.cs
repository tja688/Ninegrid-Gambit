using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 写入流程壳相位；离开战斗生命周期时请求导演 HardClear/Teardown。
    /// </summary>
    public sealed class SetGameFlowShellStateCommand : AbstractCommand
    {
        private readonly GameFlowShellState mNext;

        public SetGameFlowShellStateCommand(GameFlowShellState next)
        {
            mNext = next;
        }

        protected override void OnExecute()
        {
            var shell = GameFlowShellSystem.EnsureRegistered();
            var from = shell.State.Value;
            if (from == mNext)
            {
                return;
            }

            shell.ApplyState(mNext);
            this.SendEvent(new GameFlowShellStateChangedEvent
            {
                From = from,
                To = mNext
            });

            if (!ShouldHardClearDirector(mNext))
            {
                return;
            }

            this.SendEvent(new TeardownPresentationDirectorRequested
            {
                Reason = ResolveClearReason(mNext)
            });
        }

        private static bool ShouldHardClearDirector(GameFlowShellState next)
        {
            return next == GameFlowShellState.MainMenu
                || next == GameFlowShellState.VictoryNotice
                || next == GameFlowShellState.DefeatNotice;
        }

        private static IntentClearReason ResolveClearReason(GameFlowShellState next)
        {
            if (next == GameFlowShellState.DefeatNotice)
            {
                return IntentClearReason.Defeat;
            }

            if (next == GameFlowShellState.VictoryNotice)
            {
                return IntentClearReason.PhaseChange;
            }

            return IntentClearReason.LayerChange;
        }
    }
}
