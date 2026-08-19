namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 战败收口时序（ADR-0039）：Core 可已 Defeat / 0 血，但
    /// <c>RaiseBattleEnded</c> + <c>HardClearIntents</c> 必须等当前导演链演完。
    /// 提前 Raise 会清掉后续 Present（机关位移、打击、血条归零），死亡面板抢在致死表演之前弹出。
    /// </summary>
    public static class AvatarDefeatEndPolicy
    {
        /// <summary>
        /// 盘面 Drain 未结束或导演主线仍忙时，只武装战败收口，不得立即 Raise。
        /// </summary>
        public static bool ShouldDeferRaise(bool drainInFlight, bool mainlineBusy)
        {
            return drainInFlight || mainlineBusy;
        }
    }
}
