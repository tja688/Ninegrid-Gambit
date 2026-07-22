using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 请求遗物栏同步或清空（经 Event，由 RelicHudController 执行表现侧写入）。
    /// </summary>
    public sealed class SyncRelicHudCommand : AbstractCommand
    {
        private readonly bool mClear;

        public SyncRelicHudCommand(bool clear = false)
        {
            mClear = clear;
        }

        protected override void OnExecute()
        {
            this.SendEvent(new RelicHudSyncRequestedEvent { Clear = mClear });
        }
    }
}
