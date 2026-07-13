using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 可消费的就位时间预算：飞牌过程中逐帧扣减，耗尽后进入软着陆。
    /// </summary>
    public sealed class DealSettleBudget
    {
        public float Total { get; }
        public float Remaining { get; private set; }
        public float Consumed => Total - Remaining;
        public bool IsExhausted => Remaining <= 0f;

        public DealSettleBudget(float total)
        {
            Total = Mathf.Max(0f, total);
            Remaining = Total;
        }

        public void Consume(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            Remaining = Mathf.Max(0f, Remaining - deltaSeconds);
        }

        public void ApplyJumpPenalty(float penaltySeconds)
        {
            if (penaltySeconds <= 0f)
            {
                return;
            }

            Remaining = Mathf.Max(0f, Remaining - penaltySeconds);
        }

        public static DealSettleBudget Compute(
            Vector3 start,
            Vector3 target,
            DealFlightContext context,
            DealFlightLayoutSettings settings)
        {
            if (settings == null)
            {
                return new DealSettleBudget(0.28f);
            }

            var dist = Vector3.Distance(start, target);
            var refDist = Mathf.Max(0.01f, settings.refDistance);
            var distScale = Mathf.Pow(dist / refDist, settings.distanceExponent);
            distScale = Mathf.Clamp(distScale, settings.minDistScale, settings.maxDistScale);
            var total = settings.baseDuration * distScale;

            if (context.FieldBusy)
            {
                total += settings.busySlack;
            }

            if (context.ActiveFlightCount > 1)
            {
                total += settings.concurrentFlightSlack * (context.ActiveFlightCount - 1);
            }

            if (context.PendingRotateSteps > 0)
            {
                total += context.PendingRotateSteps * settings.perRotateConsume;
            }

            total = Mathf.Clamp(total, settings.minDuration, settings.maxDuration);
            return new DealSettleBudget(total);
        }
    }
}
