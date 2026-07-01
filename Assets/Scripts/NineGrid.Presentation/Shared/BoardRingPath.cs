using System.Collections.Generic;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Presentation.Shared
{
    /// <summary>
    /// 九宫格外圈顺时针路径（与 Core <see cref="RotateBoardClockwiseAction"/> 对齐）。
    /// </summary>
    public static class BoardRingPath
    {
        public const int CenterBoardIndex = 5;

        public static IReadOnlyList<SlotId> ClockwiseOuterRing => RotateBoardClockwiseAction.ClockwisePath;

        public static Transform ResolveBoardAnchor(Transform gridRoot, int boardIndex)
        {
            if (gridRoot == null)
            {
                return null;
            }

            string preferredName = boardIndex == CenterBoardIndex ? "slot5_Player" : $"slot{boardIndex}";
            Transform anchor = gridRoot.Find(preferredName);
            if (anchor != null)
            {
                return anchor;
            }

            return gridRoot.Find($"slot{boardIndex}");
        }

        public static bool TryResolveOuterRingAnchors(Transform gridRoot, List<Transform> outAnchors)
        {
            outAnchors.Clear();
            if (gridRoot == null)
            {
                return false;
            }

            IReadOnlyList<SlotId> path = ClockwiseOuterRing;
            for (var i = 0; i < path.Count; i++)
            {
                Transform anchor = ResolveBoardAnchor(gridRoot, path[i].Index);
                if (anchor == null)
                {
                    outAnchors.Clear();
                    return false;
                }

                outAnchors.Add(anchor);
            }

            return outAnchors.Count > 0;
        }

        public static void BuildClockwiseStepTargets(
            IReadOnlyList<Transform> ringSlots,
            int step,
            List<Transform> outTargets)
        {
            outTargets.Clear();
            if (ringSlots == null || ringSlots.Count == 0)
            {
                return;
            }

            int ringCount = ringSlots.Count;
            int direction = step >= 0 ? 1 : -1;
            for (var i = 0; i < ringCount; i++)
            {
                int targetIndex = (i + direction + ringCount) % ringCount;
                outTargets.Add(ringSlots[targetIndex]);
            }
        }
    }
}
