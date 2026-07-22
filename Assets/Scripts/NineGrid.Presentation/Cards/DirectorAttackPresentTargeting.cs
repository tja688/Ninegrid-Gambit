namespace NineGrid.Cards
{
    /// <summary>
    /// 导演攻击 Hit Present 的目标裁决：必须使用 Resolve 批捕获的 combatUid，
    /// 禁止在 Core CombatHit 之后再 Resolve（致死会卸掉嘲讽规则）。
    /// </summary>
    public static class DirectorAttackPresentTargeting
    {
        /// <summary>
        /// 由点击 UID 与解算批捕获的战斗目标 UID 决定 Present 用的 combatUid / 是否嘲讽重定向。
        /// </summary>
        public static void Decide(
            int clickedUid,
            int resolvedCombatUid,
            out int combatUid,
            out bool useTauntRedirect)
        {
            combatUid = resolvedCombatUid > 0 ? resolvedCombatUid : clickedUid;
            useTauntRedirect = clickedUid > 0 && combatUid > 0 && combatUid != clickedUid;
        }
    }
}
