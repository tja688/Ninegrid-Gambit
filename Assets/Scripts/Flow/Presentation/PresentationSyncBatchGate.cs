using System;
using NineGrid.Core;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 将既有 PresentationSyncSystem OpenBatch/FinishBatch 缝接到导演批次门。
    /// 未 ack（ActiveBatchId &gt; 0）时拒绝再解算下一批；就位回执走 FinishBatch。
    /// 与 IsInputLocked 解耦：无阻塞指令的批次仍算打开中，必须 FinishBatch 后才能下一批。
    /// </summary>
    public sealed class PresentationSyncBatchGate : IPresentationBatchGate
    {
        private readonly Func<bool> mHasOpenBatch;
        private readonly Func<int> mActiveBatchId;
        private readonly Func<CoreCommandDispatchResult> mResolveAndOpen;
        private readonly Func<int, CoreCommandResult> mFinishBatch;
        private readonly string mSlice;

        public PresentationSyncBatchGate(
            Func<bool> hasOpenBatch,
            Func<int> activeBatchId,
            Func<CoreCommandDispatchResult> resolveAndOpen,
            Func<int, CoreCommandResult> finishBatch,
            string slice = null)
        {
            if (hasOpenBatch == null)
            {
                throw new ArgumentNullException("hasOpenBatch");
            }

            if (activeBatchId == null)
            {
                throw new ArgumentNullException("activeBatchId");
            }

            if (resolveAndOpen == null)
            {
                throw new ArgumentNullException("resolveAndOpen");
            }

            if (finishBatch == null)
            {
                throw new ArgumentNullException("finishBatch");
            }

            mHasOpenBatch = hasOpenBatch;
            mActiveBatchId = activeBatchId;
            mResolveAndOpen = resolveAndOpen;
            mFinishBatch = finishBatch;
            mSlice = slice;
        }

        /// <summary>从真实 PresentationSyncSystem + Dispatcher 解算回调接线。</summary>
        public static PresentationSyncBatchGate FromSync(
            IPresentationSyncSystem sync,
            Func<CoreCommandDispatchResult> resolveAndOpen,
            string slice = null)
        {
            if (sync == null)
            {
                throw new ArgumentNullException("sync");
            }

            return new PresentationSyncBatchGate(
                () => sync.ActiveBatchId > 0,
                () => sync.ActiveBatchId,
                resolveAndOpen,
                sync.FinishBatch,
                slice);
        }

        public bool HasOpenBatch
        {
            get { return mHasOpenBatch(); }
        }

        public int ActiveBatchId
        {
            get { return mActiveBatchId(); }
        }

        public BatchOpenResult TryOpenNextBatch(out int batchId)
        {
            if (mHasOpenBatch())
            {
                batchId = 0;
                DirectorTrace.PublishActiveBatchId(mActiveBatchId());
                DirectorTrace.BatchOpenRejected(DirectorTrace.RejectHasOpen);
                return BatchOpenResult.WaitHasOpen;
            }

            var result = mResolveAndOpen();
            if (result == null || !result.Accepted)
            {
                batchId = 0;
                var detail = result != null && result.CommandResult != null
                    ? result.CommandResult.Reason
                    : "nullDispatch";
                DirectorTrace.BatchOpenRejected(DirectorTrace.RejectDispatch, detail);
                return BatchOpenResult.Failed;
            }

            if (!result.BatchOpened || result.Batch == null)
            {
                batchId = 0;
                DirectorTrace.BatchOpenRejected(DirectorTrace.RejectNoBatch);
                return BatchOpenResult.Failed;
            }

            batchId = result.Batch.BatchId;
            if (batchId <= 0)
            {
                DirectorTrace.BatchOpenRejected(DirectorTrace.RejectNoBatch);
                return BatchOpenResult.Failed;
            }

            DirectorTrace.BatchOpen(batchId, mSlice);
            return BatchOpenResult.Opened;
        }

        public bool TryAcknowledge(int batchId)
        {
            var result = mFinishBatch(batchId);
            return result != null && result.Accepted;
        }
    }
}
