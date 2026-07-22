namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Attack 在 idle 合法性门或孤儿奖励门被拒时广播，供 InBattle 做孤儿奖励 UI 恢复等旁路。
    /// </summary>
    public struct AttackIntentRejectedEvent
    {
        public int GroundSlot;
        public string Reason;
    }
}
