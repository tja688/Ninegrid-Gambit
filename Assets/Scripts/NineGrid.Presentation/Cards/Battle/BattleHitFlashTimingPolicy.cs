namespace NineGrid.Cards
{
    /// <summary>
    /// Timeline 闪白/死亡回调时机策略。
    /// Heuristic：沿用场景 Rig 烘焙 delay；Explicit：用 Profile 显式 delay 覆盖。
    /// </summary>
    public enum BattleHitFlashTimingPolicy
    {
        Heuristic = 0,
        Explicit = 1,
    }
}
