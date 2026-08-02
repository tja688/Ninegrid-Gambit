using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Flow;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 右键丢弃已装备遗物：经 Core <see cref="DiscardRelicCommand"/>，成功后同步遗物栏。
    /// </summary>
    public sealed class SubmitDiscardRelicCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly string mRelicDefId;

        public SubmitDiscardRelicCommand(string relicDefId)
        {
            mRelicDefId = relicDefId ?? string.Empty;
        }

        protected override CoreCommandResult OnExecute()
        {
            var result = this.SendCommand(new DiscardRelicCommand(mRelicDefId));
            if (result != null && result.Accepted)
            {
                RelicHudHook.RequestSync();
            }

            return result;
        }
    }
}
