using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 跳过帮助卡奖励：经 Core <see cref="SkipHelpChoiceCommand"/>。
    /// </summary>
    public sealed class SubmitSkipHelpChoiceCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.SendCommand(new SkipHelpChoiceCommand());
        }
    }
}
