using System.Collections.Generic;

namespace NineGrid.Cards
{
    /// <summary>
    /// 发牌视觉目标预解：Deal 后连续 Rotate 时，起飞即瞄准旋转后的最终格。
    /// </summary>
    public static class DealVisualTargetResolver
    {
        /// <summary>
        /// 收集 steps[startIndex..] 起连续的 Rotate 方向（遇非 Rotate 即停）。
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

            for (var i = startIndex; i < steps.Length; i++)
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
