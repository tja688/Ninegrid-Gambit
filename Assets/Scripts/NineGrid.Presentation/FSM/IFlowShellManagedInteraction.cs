namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 由流程壳启停的交互 FSM 挂点（泳道 B Board/Item；未来 Overlay）。
    /// </summary>
    public interface IFlowShellManagedInteraction
    {
        void SetFlowShellInteractionEnabled(bool enabled);
    }
}
