using System;

namespace NineGrid.Cards
{
    public readonly struct GroundFieldSlotSnapshot
    {
        public GroundFieldSlotSnapshot(int slot, int uid, bool isEmpty, bool isAvatarReserved)
        {
            Slot = slot;
            Uid = uid;
            IsEmpty = isEmpty;
            IsAvatarReserved = isAvatarReserved;
        }

        public int Slot { get; }
        public int Uid { get; }
        public bool IsEmpty { get; }
        public bool IsAvatarReserved { get; }
    }

    public sealed class GroundFieldSnapshot
    {
        public GroundFieldSnapshot(GroundFieldSlotSnapshot[] slots, int occupiedCount)
        {
            Slots = slots ?? Array.Empty<GroundFieldSlotSnapshot>();
            OccupiedCount = occupiedCount;
        }

        public GroundFieldSlotSnapshot[] Slots { get; }
        public int OccupiedCount { get; }
    }
}
