using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 请求展示原始提示文案。
    /// </summary>
    public sealed class RequestDescriptionShowTextCommand : AbstractCommand
    {
        private readonly string mText;
        private readonly DescriptionShowRoute mRoute;

        public RequestDescriptionShowTextCommand(string text, DescriptionShowRoute route)
        {
            mText = text;
            mRoute = route;
        }

        protected override void OnExecute()
        {
            this.SendEvent(new DescriptionShowTextRequested
            {
                Text = mText,
                Route = mRoute
            });
        }
    }
}
