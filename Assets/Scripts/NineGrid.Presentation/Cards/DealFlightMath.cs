using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 二阶贝塞尔飞牌纯数学（EditMode 可测）。
    /// </summary>
    public static class DealFlightMath
    {
        public static Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float u)
        {
            var t = Mathf.Clamp01(u);
            var oneMinus = 1f - t;
            return oneMinus * oneMinus * p0
                   + 2f * oneMinus * t * p1
                   + t * t * p2;
        }

        public static Vector3 ComputeControlPoint(
            Vector3 current,
            Vector3 target,
            float arcHeight,
            float distanceFactor = 1f)
        {
            var mid = Vector3.Lerp(current, target, 0.5f);
            if (arcHeight <= 0f)
            {
                return mid;
            }

            return mid + Vector3.up * arcHeight * Mathf.Max(0f, distanceFactor);
        }

        public static float ComputeSpeedFactor(
            float remainingDistance,
            float nearDistance,
            float farDistance,
            float easeNear,
            float easeFar)
        {
            if (farDistance <= nearDistance)
            {
                return easeNear;
            }

            var t = Mathf.InverseLerp(farDistance, nearDistance, remainingDistance);
            return Mathf.Lerp(easeFar, easeNear, t);
        }

        public static float ComputeBudgetFactor(float remainingBudget, float totalBudget, float minFactor = 0.15f)
        {
            if (totalBudget <= 0f)
            {
                return minFactor;
            }

            return Mathf.Clamp(remainingBudget / totalBudget, minFactor, 1f);
        }

        public static float ComputeProgressDelta(
            float baseSpeed,
            float speedFactor,
            float budgetFactor,
            float deltaTime,
            float expectedDuration)
        {
            var duration = Mathf.Max(0.01f, expectedDuration);
            return baseSpeed * speedFactor * budgetFactor * deltaTime / duration;
        }

        public static Vector3 ComputeHopMidpoint(Vector3 start, Vector3 end, float arcHeight)
        {
            var linearMid = Vector3.Lerp(start, end, 0.5f);
            if (arcHeight <= 0f)
            {
                return linearMid;
            }

            return linearMid + Vector3.up * arcHeight;
        }
    }
}
