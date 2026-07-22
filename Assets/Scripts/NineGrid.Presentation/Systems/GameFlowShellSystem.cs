using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 流程壳相位权威：BindableProperty 投影；写入经 SetGameFlowShellStateCommand。
    /// </summary>
    public sealed class GameFlowShellSystem : AbstractSystem, IGameFlowShellSystem
    {
        private readonly BindableProperty<GameFlowShellState> mState =
            new BindableProperty<GameFlowShellState>(GameFlowShellState.MainMenu);

        public IReadonlyBindableProperty<GameFlowShellState> State
        {
            get { return mState; }
        }

        public void SetState(GameFlowShellState next)
        {
            mState.Value = next;
        }

        protected override void OnInit()
        {
        }
    }
}
