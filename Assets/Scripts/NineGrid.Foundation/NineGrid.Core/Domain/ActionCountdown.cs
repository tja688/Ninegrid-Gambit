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
            return TickCount(counters, key, period, delta) > 0;
        }

        /// <summary>
        /// 按 delta 推进倒计时并返回本次跨过的阈值数。
        /// 保留跨过阈值后的余量，避免一次大 delta 吞掉多个逻辑触发。
        /// </summary>
        public static int TickCount(CounterBag counters, string key, int period, int delta)
        {
            if (counters == null || string.IsNullOrEmpty(key) || delta <= 0)
            {
                return 0;
            }

            if (period <= 1)
            {
                return delta;
            }

            var remaining = counters.Get(key);
            if (remaining <= 0)
            {
                remaining = period;
            }

            var after = (long)remaining - delta;
            if (after > 0)
            {
                counters.Set(key, (int)after);
                return 0;
            }

            var fireCount = (int)((-after) / period) + 1;
            var next = after + (long)fireCount * period;
            counters.Set(key, (int)next);
            return fireCount;
        }

        public static bool TickOnce(CounterBag counters, string key, int period)
        {
            return Tick(counters, key, period, 1);
        }
    }
}
