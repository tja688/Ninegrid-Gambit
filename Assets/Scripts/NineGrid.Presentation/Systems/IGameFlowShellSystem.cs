using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 流程壳只读投影：相位 / 节点 / run mode / generation / 门禁。
    /// 写入均经 Command；编排由内部 Orchestrator 承载。
    /// </summary>
    public interface IGameFlowShellSystem : ISystem
    {
        IReadonlyBindableProperty<GameFlowShellState> State { get; }

        int NodeIndex { get; }

        bool IsBusy { get; }

        bool IsQuickTestMode { get; }

        bool IsTutorialMode { get; }

        int Generation { get; }

        bool CanAcceptQuickTestEntry { get; }

        bool IsBound { get; }
    }
}
