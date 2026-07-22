namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Explore 在 idle 合法性门被拒时广播，供 InBattle 做孤儿奖励 UI 恢复等旁路。
    /// </summary>
    public struct ExploreIntentRejectedEvent
    {
        public int GroundSlot;
        public string Reason;
    }
}
