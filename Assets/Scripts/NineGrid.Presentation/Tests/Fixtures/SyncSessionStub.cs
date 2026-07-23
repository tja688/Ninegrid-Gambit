using NineGrid.Core;
using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    /// <summary>忠实镜像 PresentationSyncSystem OpenBatch/FinishBatch（无完整架构依赖）。</summary>
    public sealed class SyncSessionStub
    {
        private int mNextId = 1;

        public bool IsInputLocked { get; private set; }
        public int ActiveBatchId { get; private set; }

        public int NextBatchId()
        {
            return mNextId++;
        }

        public void OpenBatch(PresentationBatch batch)
        {
            ActiveBatchId = batch != null ? batch.BatchId : 0;
            IsInputLocked = batch != null && batch.RequiresAcknowledgement;
        }

        public CoreCommandResult FinishBatch(int batchId)
        {
            if (ActiveBatchId <= 0)
            {
                return CoreCommandResult.Reject("No presentation batch is waiting.");
            }

            if (batchId != ActiveBatchId)
            {
                return CoreCommandResult.Reject("Presentation batch mismatch.");
            }

            IsInputLocked = false;
            ActiveBatchId = 0;
            return CoreCommandResult.Accept(0);
        }
    }

    public static class PresentationSyncGateFactory
    {
        public static PresentationSyncBatchGate FromSession(
            SyncSessionStub session,
            System.Func<CoreCommandDispatchResult> resolveAndOpen)
        {
            return new PresentationSyncBatchGate(
                () => session.ActiveBatchId > 0,
                () => session.ActiveBatchId,
                resolveAndOpen,
                session.FinishBatch);
        }
    }
}
