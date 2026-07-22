using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：指定 EventLog 起点之后是否出现 ExistingCard 洗回事件。
    /// </summary>
    public sealed class HasShuffleExistingSinceQuery : AbstractQuery<bool>
    {
        private readonly int mStartIndex;
        private readonly ShuffleIntoDeckScheduler mScheduler;

        public HasShuffleExistingSinceQuery(int startIndex, ShuffleIntoDeckScheduler scheduler = null)
        {
            mStartIndex = startIndex;
            mScheduler = scheduler ?? new ShuffleIntoDeckScheduler();
        }

        protected override bool OnDo()
        {
            var pipeline = NineGridArchitecture.Interface.GetSystem<IActionPipelineSystem>();
            return mScheduler.HasShuffleExistingSince(pipeline, mStartIndex);
        }
    }
}
