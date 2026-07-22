using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 奖励三选一提交：经 Core <see cref="SelectRewardCommand"/>。
    /// </summary>
    public sealed class SubmitSelectRewardCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mOptionIndex;

        public SubmitSelectRewardCommand(int optionIndex)
        {
            mOptionIndex = optionIndex;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.SendCommand(new SelectRewardCommand(mOptionIndex));
        }
    }
}
