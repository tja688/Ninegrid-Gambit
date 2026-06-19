using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    public interface IPresentationSyncSystem : ISystem
    {
        bool IsInputLocked { get; }
        int ActiveBatchId { get; }
        PresentationBatch ActiveBatch { get; }
        void OpenBatch(PresentationBatch batch);
        CoreCommandResult FinishBatch(int batchId);
        void Clear();
    }

    public sealed class PresentationSyncSystem : AbstractSystem, IPresentationSyncSystem
    {
        public bool IsInputLocked { get; private set; }
        public int ActiveBatchId { get; private set; }
        public PresentationBatch ActiveBatch { get; private set; }

        protected override void OnInit()
        {
            Clear();
        }

        public void OpenBatch(PresentationBatch batch)
        {
            ActiveBatch = batch;
            ActiveBatchId = batch != null ? batch.BatchId : 0;
            IsInputLocked = batch != null && batch.RequiresAcknowledgement;
        }

        public CoreCommandResult FinishBatch(int batchId)
        {
            if (!IsInputLocked)
            {
                return CoreCommandResult.Reject("No presentation batch is waiting.");
            }

            if (batchId != ActiveBatchId)
            {
                return CoreCommandResult.Reject("Presentation batch mismatch. Expected " + ActiveBatchId + ", got " + batchId + ".");
            }

            Clear();
            return CoreCommandResult.Accept(0);
        }

        public void Clear()
        {
            IsInputLocked = false;
            ActiveBatchId = 0;
            ActiveBatch = null;
        }
    }
}
