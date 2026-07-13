namespace NineGrid.Cards
{
    /// <summary>
    /// 飞牌就位预算计算时的场地上下文。
    /// </summary>
    public readonly struct DealFlightContext
    {
        public bool FieldBusy { get; }
        public int ActiveFlightCount { get; }
        public int PendingRotateSteps { get; }

        public DealFlightContext(bool fieldBusy, int activeFlightCount, int pendingRotateSteps)
        {
            FieldBusy = fieldBusy;
            ActiveFlightCount = activeFlightCount;
            PendingRotateSteps = pendingRotateSteps;
        }
    }
}
