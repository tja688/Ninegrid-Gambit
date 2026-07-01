using NineGrid.Core;

namespace NineGrid.Presentation.Orchestration
{
    public interface IReconcilable
    {
        void ApplySnapshot(CoreViewSnapshot snapshot);
    }
}
