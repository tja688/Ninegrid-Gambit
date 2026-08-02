using System;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 表现侧场地图标占格登记（不写 BoardModel）。供寻路软占谓词与驻留提交使用。
    /// </summary>
    public sealed class RoomIconOccupancy
    {
        public static RoomIconOccupancy Current { get; private set; } = new RoomIconOccupancy();

        private readonly Dictionary<int, RoomIconEntry> mBySlot = new Dictionary<int, RoomIconEntry>(4);

        public static void ResetForTests()
        {
            Current = new RoomIconOccupancy();
        }

        public IReadOnlyDictionary<int, RoomIconEntry> BySlot => mBySlot;

        public bool HasAny => mBySlot.Count > 0;

        public void Clear()
        {
            mBySlot.Clear();
        }

        public void Register(int slot, int optionIndex, string contentId)
        {
            if (slot < SlotId.MinBoardIndex || slot > SlotId.MaxBoardIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            mBySlot[slot] = new RoomIconEntry(slot, optionIndex, contentId ?? string.Empty);
        }

        public bool TryGet(int slot, out RoomIconEntry entry)
        {
            return mBySlot.TryGetValue(slot, out entry);
        }

        public bool IsIconSlot(int slot)
        {
            return mBySlot.ContainsKey(slot);
        }

        public bool IsSoftBlocked(SlotId slot)
        {
            return slot.IsBoardSlot && mBySlot.ContainsKey(slot.Index);
        }
    }

    public readonly struct RoomIconEntry
    {
        public RoomIconEntry(int slot, int optionIndex, string contentId)
        {
            Slot = slot;
            OptionIndex = optionIndex;
            ContentId = contentId ?? string.Empty;
        }

        public int Slot { get; }
        public int OptionIndex { get; }
        public string ContentId { get; }
    }
}
