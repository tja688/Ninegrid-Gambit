using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 请求清除匹配路由的描述。
    /// </summary>
    public sealed class RequestDescriptionClearCommand : AbstractCommand
    {
        private readonly DescriptionShowRoute mRoute;

        public RequestDescriptionClearCommand(DescriptionShowRoute route)
        {
            mRoute = route;
        }

        protected override void OnExecute()
        {
            this.SendEvent(new DescriptionClearRequested
            {
                Route = mRoute
            });
        }
    }
}
