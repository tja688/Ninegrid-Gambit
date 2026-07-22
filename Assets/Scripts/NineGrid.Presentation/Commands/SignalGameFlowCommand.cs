using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 向流程编排投递 SettlementReady / BattleEnded 信号。
    /// </summary>
    public sealed class SignalGameFlowCommand : AbstractCommand
    {
        private readonly GameFlowSignal mSignal;

        public SignalGameFlowCommand(GameFlowSignal signal)
        {
            mSignal = signal;
        }

        public static SignalGameFlowCommand SettlementReady()
        {
            return new SignalGameFlowCommand(GameFlowSignal.SettlementReady());
        }

        public static SignalGameFlowCommand BattleEnded(bool victory)
        {
            return new SignalGameFlowCommand(GameFlowSignal.BattleEnded(victory));
        }

        protected override void OnExecute()
        {
            GameFlowShellSystem.EnsureRegistered().Signal(mSignal);
        }
    }
}
