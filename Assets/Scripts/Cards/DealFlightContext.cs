namespace NineGrid.Cards
{
    /// <summary>
    /// 飞牌就位预算与视觉瞄准上下文。
    /// </summary>
    public readonly struct DealFlightContext
    {
        public bool FieldBusy { get; }
        public int ActiveFlightCount { get; }
        public int PendingRotateSteps { get; }

        /// <summary>
        /// 起飞视觉目标格。≤0 表示与逻辑 birth 相同。
        /// 逻辑占格仍登记 birth；本字段只改 BeginDeal / L0 瞄准。
        /// </summary>
        public int VisualTargetSlot { get; }

        public DealFlightContext(
            bool fieldBusy,
            int activeFlightCount,
            int pendingRotateSteps,
            int visualTargetSlot = 0)
        {
            FieldBusy = fieldBusy;
            ActiveFlightCount = activeFlightCount;
            PendingRotateSteps = pendingRotateSteps;
            VisualTargetSlot = visualTargetSlot;
        }
    }
}
