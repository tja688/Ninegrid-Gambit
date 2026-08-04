using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 拖入回收区丢弃已装备遗物：经 Core <see cref="DiscardRelicCommand"/>，成功后同步遗物栏，
    /// 并经 <see cref="BattleBeatFlush"/> 冲刷 UpdateGold（与道具回收同构 UI，ADR-0007 / ADR-0027）。
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
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var logStart = pipeline?.EventLog?.Entries != null
                ? pipeline.EventLog.Entries.Count
                : 0;
            var result = this.SendCommand(new DiscardRelicCommand(mRelicDefId));
            if (result != null && result.Accepted)
            {
                RelicHudHook.RequestSync();
                BattleBeatFlush.PresentEventLogSliceOnly(
                    NineGridArchitecture.Interface ?? NineGridArchitecture.Current,
                    logStart,
                    PresentationInstructionKind.UpdateGold);
            }

            return result;
        }
    }
}
