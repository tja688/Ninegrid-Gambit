using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 融合伴随补牌：经导演锁步同一 Resolve 入口写 Core 并投影盘面摘要。
    /// </summary>
    public sealed class ResolveFusionRefillBatchCommand : AbstractCommand<CoreCommandDispatchResult>
    {
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly int mBoardSlot;
        private readonly IReadOnlyList<int> mExcludeResultUids;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBoardBatchProjected;
        private readonly FusionRefillScheduler mScheduler;

        public ResolveFusionRefillBatchCommand(
            CoreCommandDispatcher dispatcher,
            int boardSlot,
            IReadOnlyList<int> excludeResultUids,
            Action<int, int, PostKillBoardPresentationResult> onBoardBatchProjected = null,
            FusionRefillScheduler scheduler = null)
        {
            mDispatcher = dispatcher;
            mBoardSlot = boardSlot;
            mExcludeResultUids = excludeResultUids;
            mOnBoardBatchProjected = onBoardBatchProjected;
            mScheduler = scheduler ?? new FusionRefillScheduler();
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
                mExcludeResultUids,
                mOnBoardBatchProjected);
        }
    }
}
