using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    // Events: Evt_PresentationBatchOpened / Evt_PresentationBatchCleared / Evt_PresentationBatchFinishRejected
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
            if (batch != null)
            {
                this.SendEvent(new Evt_PresentationBatchOpened(batch));
            }
        }

        public CoreCommandResult FinishBatch(int batchId)
        {
            if (!IsInputLocked)
            {
                return CoreCommandResult.Reject("No presentation batch is waiting.");
            }

            if (batchId != ActiveBatchId)
            {
                var reason = "Presentation batch mismatch. Expected " + ActiveBatchId + ", got " + batchId + ".";
                this.SendEvent(new Evt_PresentationBatchFinishRejected(ActiveBatchId, batchId, reason));
                return CoreCommandResult.Reject(reason);
            }

            Clear(acknowledged: true);
            return CoreCommandResult.Accept(0);
        }

        public void Clear()
        {
            Clear(acknowledged: false);
        }

        private void Clear(bool acknowledged)
        {
            var clearedBatchId = ActiveBatchId;
            IsInputLocked = false;
            ActiveBatchId = 0;
            ActiveBatch = null;
            if (clearedBatchId > 0)
            {
                this.SendEvent(new Evt_PresentationBatchCleared(clearedBatchId, acknowledged));
            }
        }
    }
}
