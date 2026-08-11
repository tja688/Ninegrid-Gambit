using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// #199 HUD 金币数字时间窗纯数学：首达前保持起点，末达精确收敛到 AmountAfter；
    /// 单调整数曲线，不依赖逐金币回调。
    /// </summary>
    public static class GoldHudNumberWindow
    {
        /// <summary>
        /// 按首达→末达窗口采样显示值。elapsed 相对批次开始；末达时恒为 amountAfter。
        /// </summary>
        public static int SampleDisplayed(
            int amountBefore,
            int amountAfter,
            float firstArrivalDelay,
            float lastArrivalDelay,
            float elapsed)
        {
            amountBefore = Mathf.Max(0, amountBefore);
            amountAfter = Mathf.Max(0, amountAfter);

            if (!IsValidWindow(firstArrivalDelay, lastArrivalDelay))
            {
                return amountAfter;
            }

            if (elapsed < firstArrivalDelay)
            {
                return amountBefore;
            }

            if (elapsed >= lastArrivalDelay)
            {
                return amountAfter;
            }

            var span = lastArrivalDelay - firstArrivalDelay;
            if (span <= 1e-6f)
            {
                return amountAfter;
            }

            var t = Mathf.Clamp01((elapsed - firstArrivalDelay) / span);
            var eased = EaseOutQuad(t);
            return Mathf.RoundToInt(Mathf.Lerp(amountBefore, amountAfter, eased));
        }

        public static bool IsValidWindow(float firstArrivalDelay, float lastArrivalDelay)
        {
            return firstArrivalDelay > 0f && lastArrivalDelay >= firstArrivalDelay;
        }

        private static float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }
    }
}
