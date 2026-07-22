using NineGrid.Cards;

namespace NineGrid.Flow.Presentation
{
    /// <summary>请求展示 defId 对应描述（HUD/选择提示，非卡面 Commit 权威）。</summary>
    public struct DescriptionShowRequested
    {
        public string DefId;
        public DescriptionShowRoute Route;
    }

    /// <summary>请求展示原始提示文案。</summary>
    public struct DescriptionShowTextRequested
    {
        public string Text;
        public DescriptionShowRoute Route;
    }

    /// <summary>请求清除匹配路由的描述。</summary>
    public struct DescriptionClearRequested
    {
        public DescriptionShowRoute Route;
    }
}
