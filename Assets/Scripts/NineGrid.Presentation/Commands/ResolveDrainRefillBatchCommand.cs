using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// drain 退场补牌：经导演锁步同一 Resolve 入口写 Core 并投影盘面摘要。
    /// </summary>
    public sealed class ResolveDrainRefillBatchCommand : AbstractCommand<CoreCommandDispatchResult>
    {
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly int mBoardSlot;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
        private readonly DrainRefillScheduler mScheduler;

        public ResolveDrainRefillBatchCommand(
            CoreCommandDispatcher dispatcher,
            int boardSlot,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected = null,
            DrainRefillScheduler scheduler = null)
        {
            mDispatcher = dispatcher;
            mBoardSlot = boardSlot;
            mOnBoardBatchProjected = onBoardBatchProjected;
            mScheduler = scheduler ?? new DrainRefillScheduler();
        }

        protected override CoreCommandDispatchResult OnExecute()
        {
            if (mDispatcher == null)
            {
                return null;
            }

            return mScheduler.ResolveAndProject(
                NineGridArchitecture.Interface,
                mDispatcher,
                mBoardSlot,
                mOnBoardBatchProjected);
        }
    }
}
