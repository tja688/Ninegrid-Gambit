using System;
using System.Collections.Generic;

namespace NineGrid.Cards
{
    /// <summary>
    /// 九宫格方位静态查表（1-based 格号 1~9）。
    /// </summary>
    public static class GroundSlotTopology
    {
        public const int MinSlot = 1;
        public const int MaxSlot = 9;
        public const int CenterSlot = 5;
        public const int AvatarReservedSlot = CenterSlot;

        public static readonly IReadOnlyList<int> ClockwiseRing = new[] { 1, 2, 3, 6, 9, 8, 7, 4 };

        private static readonly byte[] RowOfSlot = { 0, 0, 0, 0, 1, 1, 1, 2, 2, 2 };
        private static readonly byte[] ColOfSlot = { 0, 0, 1, 2, 0, 1, 2, 0, 1, 2 };
        private static readonly ushort[] OrthoMask = new ushort[10];
        private static readonly ushort[] DiagMask = new ushort[10];
        private static readonly int[] ClockwiseRingIndex = new int[10];

        static GroundSlotTopology()
        {
            for (var slot = MinSlot; slot <= MaxSlot; slot++)
            {
                OrthoMask[slot] = BuildNeighborMask(slot, IsOrthogonalNeighbor);
                DiagMask[slot] = BuildNeighborMask(slot, IsDiagonalNeighbor);
            }

            for (var i = 0; i < ClockwiseRingIndex.Length; i++)
            {
                ClockwiseRingIndex[i] = -1;
            }

            for (var i = 0; i < ClockwiseRing.Count; i++)
            {
                ClockwiseRingIndex[ClockwiseRing[i]] = i;
            }
        }

        public static bool IsValidSlot(int slot)
        {
            return slot >= MinSlot && slot <= MaxSlot;
        }

        public static bool IsCenter(int slot)
        {
            return slot == CenterSlot;
        }

        public static bool IsCorner(int slot)
        {
            return slot is 1 or 3 or 7 or 9;
        }

        public static bool IsOuterRing(int slot)
        {
            return IsValidSlot(slot) && ClockwiseRingIndex[slot] >= 0;
        }

        public static bool IsAvatarReserved(int slot)
        {
            return slot == AvatarReservedSlot;
        }

        public static int GetRow(int slot)
        {
            EnsureValid(slot);
            return RowOfSlot[slot];
        }

        public static int GetColumn(int slot)
        {
            EnsureValid(slot);
            return ColOfSlot[slot];
        }

        public static int GetClockwiseRingIndex(int slot)
        {
            if (!IsValidSlot(slot))
            {
                return -1;
            }

            return ClockwiseRingIndex[slot];
        }

        public static bool AreOrthogonal(int fromSlot, int toSlot)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot))
            {
                return false;
            }

            return (OrthoMask[fromSlot] & (1 << toSlot)) != 0;
        }

        public static bool AreDiagonal(int fromSlot, int toSlot)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot))
            {
                return false;
            }

            return (DiagMask[fromSlot] & (1 << toSlot)) != 0;
        }

        /// <summary>八向相邻（正交或对角）。敌方斜角近战 Present 资格用；玩家主动开战仍只认正交。</summary>
        public static bool AreAdjacentEight(int fromSlot, int toSlot)
        {
            return AreOrthogonal(fromSlot, toSlot) || AreDiagonal(fromSlot, toSlot);
        }

        public static GroundSlotRelation QueryRelation(int fromSlot, int toSlot)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot))
            {
                return GroundSlotRelation.None;
            }

            if (fromSlot == toSlot)
            {
                var self = GroundSlotRelation.Self;
                if (IsCorner(fromSlot))
                {
                    self |= GroundSlotRelation.Corner;
                }

                if (IsOuterRing(fromSlot))
                {
                    self |= GroundSlotRelation.OuterRing;
                }

                return self;
            }

            var relation = GroundSlotRelation.None;
            if (AreOrthogonal(fromSlot, toSlot))
            {
                relation |= GroundSlotRelation.Orthogonal;
            }

            if (AreDiagonal(fromSlot, toSlot))
            {
                relation |= GroundSlotRelation.Diagonal;
            }

            if (RowOfSlot[fromSlot] == RowOfSlot[toSlot])
            {
                relation |= GroundSlotRelation.SameRow;
            }

            if (ColOfSlot[fromSlot] == ColOfSlot[toSlot])
            {
                relation |= GroundSlotRelation.SameColumn;
            }

            if (IsCorner(toSlot))
            {
                relation |= GroundSlotRelation.Corner;
            }

            if (IsOuterRing(toSlot))
            {
                relation |= GroundSlotRelation.OuterRing;
            }

            return relation;
        }

        public static IReadOnlyList<int> GetNeighbors(int slot, GroundSlotRelation filter)
        {
            if (!IsValidSlot(slot))
            {
                return Array.Empty<int>();
            }

            var result = new List<int>(8);
            for (var candidate = MinSlot; candidate <= MaxSlot; candidate++)
            {
                if (candidate == slot)
                {
                    continue;
                }

                var relation = QueryRelation(slot, candidate);
                if ((relation & filter) != 0)
                {
                    result.Add(candidate);
                }
            }

            return result;
        }

        public static int GetClockwiseRingTargetSlot(int fromSlot, bool clockwise = true)
        {
            var index = GetClockwiseRingIndex(fromSlot);
            if (index < 0)
            {
                return fromSlot;
            }

            var count = ClockwiseRing.Count;
            var nextIndex = clockwise
                ? (index + 1) % count
                : (index + count - 1) % count;
            return ClockwiseRing[nextIndex];
        }

        private static ushort BuildNeighborMask(int slot, Func<int, int, bool> predicate)
        {
            ushort mask = 0;
            for (var candidate = MinSlot; candidate <= MaxSlot; candidate++)
            {
                if (candidate != slot && predicate(slot, candidate))
                {
                    mask |= (ushort)(1 << candidate);
                }
            }

            return mask;
        }

        private static bool IsOrthogonalNeighbor(int fromSlot, int toSlot)
        {
            return Math.Abs(RowOfSlot[fromSlot] - RowOfSlot[toSlot])
                   + Math.Abs(ColOfSlot[fromSlot] - ColOfSlot[toSlot]) == 1;
        }

        private static bool IsDiagonalNeighbor(int fromSlot, int toSlot)
        {
            var rowDelta = Math.Abs(RowOfSlot[fromSlot] - RowOfSlot[toSlot]);
            var colDelta = Math.Abs(ColOfSlot[fromSlot] - ColOfSlot[toSlot]);
            return rowDelta == 1 && colDelta == 1;
        }

        private static void EnsureValid(int slot)
        {
            if (!IsValidSlot(slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Board slot must be 1..9.");
            }
        }
    }
}
