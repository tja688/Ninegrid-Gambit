using NineGrid.Core;
using NineGrid.Presentation.Visuals;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class StatusPanelReconcilable : IReconcilable
    {
        private readonly TableNineStatusPanelView mStatusPanel;

        public StatusPanelReconcilable(TableNineStatusPanelView statusPanel)
        {
            mStatusPanel = statusPanel;
        }

        public void ApplySnapshot(CoreViewSnapshot snapshot)
        {
            mStatusPanel?.ApplySnapshot(snapshot);
        }
    }
}
