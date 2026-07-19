using System;
using NineGrid.Core;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 将既有 PresentationSyncSystem OpenBatch/FinishBatch 缝接到导演批次门。
    /// 未 ack（IsInputLocked）时拒绝再解算下一批；就位回执走 FinishBatch。
    /// </summary>
    public sealed class PresentationSyncBatchGate : IPresentationBatchGate
    {
        private readonly Func<bool> mIsInputLocked;
        private readonly Func<int> mActiveBatchId;
        private readonly Func<CoreCommandDispatchResult> mResolveAndOpen;
        private readonly Func<int, CoreCommandResult> mFinishBatch;

        public PresentationSyncBatchGate(
            Func<bool> isInputLocked,
            Func<int> activeBatchId,
            Func<CoreCommandDispatchResult> resolveAndOpen,
            Func<int, CoreCommandResult> finishBatch)
        {
            if (isInputLocked == null)
            {
                throw new ArgumentNullException("isInputLocked");
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

            mIsInputLocked = isInputLocked;
            mActiveBatchId = activeBatchId;
            mResolveAndOpen = resolveAndOpen;
            mFinishBatch = finishBatch;
        }

        /// <summary>从真实 PresentationSyncSystem + Dispatcher 解算回调接线。</summary>
        public static PresentationSyncBatchGate FromSync(
            IPresentationSyncSystem sync,
            Func<CoreCommandDispatchResult> resolveAndOpen)
        {
            if (sync == null)
            {
                throw new ArgumentNullException("sync");
            }

            return new PresentationSyncBatchGate(
                () => sync.IsInputLocked,
                () => sync.ActiveBatchId,
                resolveAndOpen,
                sync.FinishBatch);
        }

        public bool HasOpenBatch
        {
            get { return mIsInputLocked(); }
        }

        public int ActiveBatchId
        {
            get { return mActiveBatchId(); }
        }

        public bool TryOpenNextBatch(out int batchId)
        {
            if (mIsInputLocked())
            {
                batchId = 0;
                return false;
            }

            var result = mResolveAndOpen();
            if (result == null || !result.Accepted || !result.BatchOpened || result.Batch == null)
            {
                batchId = 0;
                return false;
            }

            batchId = result.Batch.BatchId;
            return batchId > 0;
        }

        public bool TryAcknowledge(int batchId)
        {
            var result = mFinishBatch(batchId);
            return result != null && result.Accepted;
        }
    }
}
