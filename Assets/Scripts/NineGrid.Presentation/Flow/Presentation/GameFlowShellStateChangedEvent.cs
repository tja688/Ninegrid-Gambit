namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 流程壳相位变更广播（BindableProperty 之外的一次性通知）。
    /// </summary>
    public struct GameFlowShellStateChangedEvent
    {
        public GameFlowShellState From;
        public GameFlowShellState To;
    }
}
