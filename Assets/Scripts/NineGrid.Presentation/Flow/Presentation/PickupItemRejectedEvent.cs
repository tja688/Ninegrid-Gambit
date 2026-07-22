namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Pickup 在 Core 写被拒时广播。
    /// </summary>
    public struct PickupItemRejectedEvent
    {
        public int GroundSlot;
        public string Reason;
    }
}