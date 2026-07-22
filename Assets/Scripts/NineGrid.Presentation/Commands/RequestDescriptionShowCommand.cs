using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 请求展示 defId 描述（HUD/提示，非卡面 Commit 权威）。
    /// </summary>
    public sealed class RequestDescriptionShowCommand : AbstractCommand
    {
        private readonly string mDefId;
        private readonly DescriptionShowRoute mRoute;

        public RequestDescriptionShowCommand(string defId, DescriptionShowRoute route)
        {
            mDefId = defId;
            mRoute = route;
        }

        protected override void OnExecute()
        {
            this.SendEvent(new DescriptionShowRequested
            {
                DefId = mDefId,
                Route = mRoute
            });
        }
    }
}
