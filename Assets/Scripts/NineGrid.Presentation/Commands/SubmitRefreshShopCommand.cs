using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 商店刷新：经 Core <see cref="RefreshShopCommand"/>。
    /// </summary>
    public sealed class SubmitRefreshShopCommand : AbstractCommand<CoreCommandResult>
    {
        protected override CoreCommandResult OnExecute()
        {
            return this.SendCommand(new RefreshShopCommand());
        }
    }
}
