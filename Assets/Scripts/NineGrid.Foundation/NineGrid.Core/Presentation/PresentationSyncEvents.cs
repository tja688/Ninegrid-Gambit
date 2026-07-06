namespace NineGrid.Core
{
    public sealed class Evt_PresentationBatchOpened
    {
        public Evt_PresentationBatchOpened(PresentationBatch batch)
        {
            Batch = batch;
            BatchId = batch != null ? batch.BatchId : 0;
        }

        public PresentationBatch Batch { get; private set; }
        public int BatchId { get; private set; }
    }

    public sealed class Evt_PresentationBatchCleared
    {
        public Evt_PresentationBatchCleared(int batchId, bool wasAcknowledged)
        {
            BatchId = batchId;
            WasAcknowledged = wasAcknowledged;
        }

        public int BatchId { get; private set; }
        public bool WasAcknowledged { get; private set; }
    }

    public sealed class Evt_PresentationBatchFinishRejected
    {
        public Evt_PresentationBatchFinishRejected(int expectedBatchId, int receivedBatchId, string reason)
        {
            ExpectedBatchId = expectedBatchId;
            ReceivedBatchId = receivedBatchId;
            Reason = reason ?? string.Empty;
        }

        public int ExpectedBatchId { get; private set; }
        public int ReceivedBatchId { get; private set; }
        public string Reason { get; private set; }
    }
}
