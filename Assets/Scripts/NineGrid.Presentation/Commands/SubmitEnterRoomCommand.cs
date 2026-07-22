using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 进入已选房间：经 Core <see cref="EnterRoomCommand"/>。
    /// </summary>
    public sealed class SubmitEnterRoomCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.SendCommand(new EnterRoomCommand());
        }
    }
}
