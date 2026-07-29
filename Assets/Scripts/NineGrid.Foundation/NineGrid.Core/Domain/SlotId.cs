using System;

namespace NineGrid.Core
{
    public struct SlotId : IEquatable<SlotId>
    {
        public const int NoneIndex = -1;
        public const int AvatarIndex = 0;
        public const int MinBoardIndex = 1;
        public const int MaxBoardIndex = 9;

        private readonly int mIndex;

        private SlotId(int index)
        {
            mIndex = index;
        }

        public int Index
        {
            get { return mIndex; }
        }

        public bool IsNone
        {
            get { return mIndex == NoneIndex; }
        }

        public bool IsAvatar
        {
            get { return mIndex == AvatarIndex; }
        }

        public bool IsBoardSlot
        {
            get { return mIndex >= MinBoardIndex && mIndex <= MaxBoardIndex; }
        }

        public int Row
        {
            get
            {
                EnsureBoardSlot();
                return (mIndex - 1) / 3;
            }
        }

        public int Column
        {
            get
            {
                EnsureBoardSlot();
                return (mIndex - 1) % 3;
            }
        }

        public static SlotId None
        {
            get { return new SlotId(NoneIndex); }
        }

        public static SlotId Avatar
        {
            get { return new SlotId(AvatarIndex); }
        }

        public static SlotId Board(int index)
        {
            if (index < MinBoardIndex || index > MaxBoardIndex)
            {
                throw new ArgumentOutOfRangeException("index", "Board slot index must be 1..9.");
            }

            return new SlotId(index);
        }

        public bool IsAdjacentTo(SlotId other)
        {
            if (!IsBoardSlot || !other.IsBoardSlot)
            {
                return false;
            }

            return Math.Abs(Row - other.Row) + Math.Abs(Column - other.Column) == 1;
        }

        /// <summary>
        /// 对角相邻（行列差均为 1）。正交邻接见 <see cref="IsAdjacentTo"/>（ADR-0011）。
        /// </summary>
        public bool IsDiagonallyAdjacentTo(SlotId other)
        {
            if (!IsBoardSlot || !other.IsBoardSlot)
            {
                return false;
            }

            return Math.Abs(Row - other.Row) == 1 && Math.Abs(Column - other.Column) == 1;
        }

        public bool Equals(SlotId other)
        {
            return mIndex == other.mIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is SlotId && Equals((SlotId)obj);
        }

        public override int GetHashCode()
        {
            return mIndex;
        }

        public override string ToString()
        {
            if (IsNone)
            {
                return "None";
            }

            if (IsAvatar)
            {
                return "Avatar";
            }

            return "Board" + mIndex;
        }

        public static bool operator ==(SlotId left, SlotId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SlotId left, SlotId right)
        {
            return !left.Equals(right);
        }

        private void EnsureBoardSlot()
        {
            if (!IsBoardSlot)
            {
                throw new InvalidOperationException("Slot is not a board slot: " + ToString());
            }
        }
    }
}
