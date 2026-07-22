namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// UseItem 在 idle 合法性门被拒时广播。
    /// </summary>
    public struct UseItemIntentRejectedEvent
    {
        public int ItemUid;
        public string Reason;
    }
}
