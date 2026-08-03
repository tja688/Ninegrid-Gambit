using System;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 表现侧场地软占登记（不写 BoardModel）。
    /// <see cref="RoomIconWalkRole.WalkDestination"/>：离开/导航图标，可作 BoardWalk 终点并驻留。
    /// <see cref="RoomIconWalkRole.SoftBlockOnly"/>：货架/就地选项/刷新/候选——只挡途经优先空路，禁止落格终点（ADR-0020 任意距离点击）。
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
            Register(slot, optionIndex, contentId, RoomIconWalkRole.WalkDestination);
        }

        public void Register(int slot, int optionIndex, string contentId, RoomIconWalkRole walkRole)
        {
            if (slot < SlotId.MinBoardIndex || slot > SlotId.MaxBoardIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            mBySlot[slot] = new RoomIconEntry(slot, optionIndex, contentId ?? string.Empty, walkRole);
        }

        public bool TryGet(int slot, out RoomIconEntry entry)
        {
            return mBySlot.TryGetValue(slot, out entry);
        }

        public bool IsIconSlot(int slot)
        {
            return mBySlot.ContainsKey(slot);
        }

        /// <summary>任意软占（货架/选项/图标），途经 PreferEmpty 时绕开。</summary>
        public bool IsSoftBlocked(SlotId slot)
        {
            return slot.IsBoardSlot && mBySlot.ContainsKey(slot.Index);
        }

        /// <summary>仅离开/导航等可驻留提交的图标格，可作为 BoardWalk 终点。</summary>
        public bool IsWalkDestination(SlotId slot)
        {
            return slot.IsBoardSlot
                   && mBySlot.TryGetValue(slot.Index, out var entry)
                   && entry.WalkRole == RoomIconWalkRole.WalkDestination;
        }

        public bool IsWalkDestination(int slot)
        {
            return mBySlot.TryGetValue(slot, out var entry)
                   && entry.WalkRole == RoomIconWalkRole.WalkDestination;
        }
    }

    public enum RoomIconWalkRole
    {
        /// <summary>可 BoardWalk 落格 + 驻留提交（离开 / 选房导航图标）。</summary>
        WalkDestination = 0,

        /// <summary>仅软占：任意距离点击，禁止作为跳格终点。</summary>
        SoftBlockOnly = 1,
    }

    public readonly struct RoomIconEntry
    {
        public RoomIconEntry(int slot, int optionIndex, string contentId)
            : this(slot, optionIndex, contentId, RoomIconWalkRole.WalkDestination)
        {
        }

        public RoomIconEntry(int slot, int optionIndex, string contentId, RoomIconWalkRole walkRole)
        {
            Slot = slot;
            OptionIndex = optionIndex;
            ContentId = contentId ?? string.Empty;
            WalkRole = walkRole;
        }

        public int Slot { get; }
        public int OptionIndex { get; }
        public string ContentId { get; }
        public RoomIconWalkRole WalkRole { get; }
    }
}
