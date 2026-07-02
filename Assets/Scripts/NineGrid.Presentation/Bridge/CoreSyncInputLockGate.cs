using NineGrid.Core;

namespace NineGrid.Presentation.Bridge
{
    /// <summary>
    /// 将 Core <see cref="IPresentationSyncSystem.IsInputLocked"/> 暴露为表现层输入闸门。
    /// 批次开启/关闭由 <see cref="CommandGateway"/> 经 Dispatcher 驱动。
    /// </summary>
    public sealed class CoreSyncInputLockGate : Orchestration.IInputLockGate
    {
        private readonly IPresentationSyncSystem mSync;

        public CoreSyncInputLockGate(IPresentationSyncSystem sync)
        {
            mSync = sync;
        }

        public bool IsLocked => mSync != null && mSync.IsInputLocked;

        public void Acquire(int batchId)
        {
        }

        public void Release(int batchId)
        {
        }
    }
}
