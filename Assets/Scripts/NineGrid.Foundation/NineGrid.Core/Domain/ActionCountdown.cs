namespace NineGrid.Core
{
    /// <summary>
    /// ADR-0013：「每 N 次」统一为倒计时语义。计数器存「还差几次」；
    /// 未初始化（≤0）时置为 period，每次按 delta 减量，≤0 则触发并加回 period。
    /// </summary>
    public static class ActionCountdown
    {
        /// <summary>
        /// 按 delta 推进倒计时。period ≤ 1 时任意正 delta 直接触发且不碰计数器。
        /// </summary>
        public static bool Tick(CounterBag counters, string key, int period, int delta)
        {
            if (counters == null || string.IsNullOrEmpty(key) || delta <= 0)
            {
                return false;
            }

            if (period <= 1)
            {
                return true;
            }

            var remaining = counters.Get(key);
            if (remaining <= 0)
            {
                remaining = period;
            }

            remaining -= delta;
            if (remaining > 0)
            {
                counters.Set(key, remaining);
                return false;
            }

            counters.Set(key, remaining + period);
            return true;
        }

        public static bool TickOnce(CounterBag counters, string key, int period)
        {
            return Tick(counters, key, period, 1);
        }
    }
}
