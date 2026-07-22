namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 分型输入门禁处置：避免万能 IsBusy 抹平 Explore / Attack / Pickup 等不同语义。
    /// </summary>
    public enum PresentationInputDisposition
    {
        Allow = 0,
        BufferToDirector = 1,
        RouteToBoardSelect = 2,
        Reject = 3,
    }

    public readonly struct PresentationInputGateResult
    {
        public PresentationInputDisposition Disposition { get; }
        public string Reason { get; }

        public bool IsAllowed =>
            Disposition == PresentationInputDisposition.Allow
            || Disposition == PresentationInputDisposition.BufferToDirector;

        private PresentationInputGateResult(PresentationInputDisposition disposition, string reason)
        {
            Disposition = disposition;
            Reason = reason;
        }

        public static PresentationInputGateResult Allow()
        {
            return new PresentationInputGateResult(PresentationInputDisposition.Allow, null);
        }

        public static PresentationInputGateResult BufferToDirector(string reason = null)
        {
            return new PresentationInputGateResult(
                PresentationInputDisposition.BufferToDirector,
                reason);
        }

        public static PresentationInputGateResult RouteToBoardSelect()
        {
            return new PresentationInputGateResult(
                PresentationInputDisposition.RouteToBoardSelect,
                "boardSelect");
        }

        public static PresentationInputGateResult Reject(string reason)
        {
            return new PresentationInputGateResult(
                PresentationInputDisposition.Reject,
                reason ?? "rejected");
        }
    }
}
