namespace NineGrid.Presentation.Orchestration
{
    public sealed class LocalInputLockGate : IInputLockGate
    {
        private int mActiveBatchId;

        public bool IsLocked { get; private set; }

        public void Acquire(int batchId)
        {
            IsLocked = true;
            mActiveBatchId = batchId;
        }

        public void Release(int batchId)
        {
            if (mActiveBatchId == batchId)
            {
                IsLocked = false;
                mActiveBatchId = 0;
            }
        }
    }
}
