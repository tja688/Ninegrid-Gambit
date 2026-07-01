namespace NineGrid.Presentation.Orchestration
{
    public interface IInputLockGate
    {
        bool IsLocked { get; }
        void Acquire(int batchId);
        void Release(int batchId);
    }
}
