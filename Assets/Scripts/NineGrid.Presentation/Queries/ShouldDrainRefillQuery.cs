using NineGrid.Core;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：当前是否应追加独立 DrainRefill 批次（InteractionLoop + 牌堆有牌 + 盘面有空位）。
    /// </summary>
    public sealed class ShouldDrainRefillQuery : AbstractQuery<bool>
    {
        private readonly DrainRefillScheduler mScheduler;

        public ShouldDrainRefillQuery(DrainRefillScheduler scheduler = null)
        {
            mScheduler = scheduler ?? new DrainRefillScheduler();
        }

        protected override bool OnDo()
        {
            return mScheduler.ShouldRefill(NineGridArchitecture.Interface);
        }
    }
}
