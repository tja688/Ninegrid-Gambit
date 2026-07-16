using System.Collections.Generic;

namespace NineGrid.Cards
{
    /// <summary>
    /// 发牌视觉目标预解：同批 Fill 的多张 Deal 跳过中间 Deal step，起飞即瞄准后续 Rotate 后的最终格。
    /// </summary>
    public static class DealVisualTargetResolver
    {
        /// <summary>
        /// 收集 steps[startIndex..] 起的 pending Rotate 方向。
        /// 跳过连续 Deal（同批 Fill 多张补牌），遇到 Rotate 则连续收集；
        /// 遇到 Move/Swap/Remove/其它则停（不过度跨越无关 step）。
        /// </summary>
        public static void CollectPendingRotateDirections(
            BoardPresentationStep[] steps,
            int startIndex,
            List<bool> into)
        {
            into?.Clear();
            if (into == null || steps == null || startIndex < 0)
            {
                return;
            }

            var i = startIndex;
            while (i < steps.Length && steps[i].Kind == BoardPresentationStepKind.Deal)
            {
                i++;
            }

            for (; i < steps.Length; i++)
            {
                if (steps[i].Kind != BoardPresentationStepKind.Rotate)
                {
                    break;
                }

                into.Add(steps[i].Clockwise);
            }
        }

        /// <summary>
        /// 对 birthSlot 依次应用环移方向，得到视觉最终格。
        /// </summary>
        public static int ApplyRingSteps(int birthSlot, IReadOnlyList<bool> clockwisePerStep)
        {
            var slot = birthSlot;
            if (clockwisePerStep == null || clockwisePerStep.Count == 0)
            {
                return slot;
            }

            for (var i = 0; i < clockwisePerStep.Count; i++)
            {
                slot = GroundSlotTopology.GetClockwiseRingTargetSlot(slot, clockwisePerStep[i]);
            }

            return slot;
        }

        /// <summary>
        /// 一步解析：后续连续 Rotate → 视觉最终格 + 步数（供预算松弛）。
        /// </summary>
        public static int ResolveVisualDealSlot(
            int birthSlot,
            BoardPresentationStep[] steps,
            int rotateScanStart,
            out int pendingRotateSteps)
        {
            var directions = new List<bool>(4);
            CollectPendingRotateDirections(steps, rotateScanStart, directions);
            pendingRotateSteps = directions.Count;
            return ApplyRingSteps(birthSlot, directions);
        }
    }
}
