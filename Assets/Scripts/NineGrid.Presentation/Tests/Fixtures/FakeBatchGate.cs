using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    public sealed class FakeBatchGate : IPresentationBatchGate
    {
        private int mNextId = 1;
        private readonly bool mAlwaysFail;

        public FakeBatchGate(bool alwaysFail = false)
        {
            mAlwaysFail = alwaysFail;
        }

        public bool HasOpenBatch { get; private set; }
        public int ActiveBatchId { get; private set; }

        public BatchOpenResult TryOpenNextBatch(out int batchId)
        {
            if (mAlwaysFail)
            {
                batchId = 0;
                return BatchOpenResult.Failed;
            }

            if (HasOpenBatch)
            {
                batchId = 0;
                return BatchOpenResult.WaitHasOpen;
            }

            batchId = mNextId++;
            ActiveBatchId = batchId;
            HasOpenBatch = true;
            return BatchOpenResult.Opened;
        }

        public bool TryAcknowledge(int batchId)
        {
            if (!HasOpenBatch || batchId != ActiveBatchId)
            {
                return false;
            }

            HasOpenBatch = false;
            ActiveBatchId = 0;
            return true;
        }
    }
}
