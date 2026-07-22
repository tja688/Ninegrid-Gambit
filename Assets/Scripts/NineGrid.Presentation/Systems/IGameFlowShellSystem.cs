using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    public interface IGameFlowShellSystem : ISystem
    {
        IReadonlyBindableProperty<GameFlowShellState> State { get; }

        void SetState(GameFlowShellState next);
    }
}
