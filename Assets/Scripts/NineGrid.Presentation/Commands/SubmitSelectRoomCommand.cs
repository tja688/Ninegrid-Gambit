using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 房间二选一提交：经 Core <see cref="SelectRoomCommand"/>。
    /// </summary>
    public sealed class SubmitSelectRoomCommand : AbstractCommand<CoreCommandResult>
    {
        private readonly int mOptionIndex;

        public SubmitSelectRoomCommand(int optionIndex)
        {
            mOptionIndex = optionIndex;
        }

        protected override CoreCommandResult OnExecute()
        {
            return this.SendCommand(new SelectRoomCommand(mOptionIndex));
        }
    }
}
