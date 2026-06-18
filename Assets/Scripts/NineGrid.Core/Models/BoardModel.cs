using System;
using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core
{
    public sealed class BoardModel : AbstractModel
    {
        private readonly int[] mCardUidsBySlot = new int[SlotId.MaxBoardIndex + 1];
        private readonly bool[] mBlessedSlots = new bool[SlotId.MaxBoardIndex + 1];

        public BindableProperty<int> Version { get; private set; }
        public BindableProperty<int> AvatarUid { get; private set; }
        public BindableProperty<SlotId> AvatarSlot { get; private set; }

        protected override void OnInit()
        {
            if (Version == null)
            {
                Version = new BindableProperty<int>(0);
                AvatarUid = new BindableProperty<int>(0);
                AvatarSlot = new BindableProperty<SlotId>(SlotId.None);
            }
        }

        public int GetCardUid(SlotId slot)
        {
            EnsureBoardSlot(slot);
            return mCardUidsBySlot[slot.Index];
        }

        public bool IsEmpty(SlotId slot)
        {
            return GetCardUid(slot) == 0;
        }

        public IEnumerable<int> BoardCardUids()
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                if (mCardUidsBySlot[i] != 0)
                {
                    yield return mCardUidsBySlot[i];
                }
            }
        }

        public void SetAvatar(CardInstance avatar, SlotId slot)
        {
            if (avatar == null)
            {
                throw new ArgumentNullException("avatar");
            }

            EnsureBoardSlot(slot);
            AvatarUid.Value = avatar.Uid;
            AvatarSlot.Value = slot;
            avatar.Zone.Value = ZoneId.Avatar;
            avatar.Slot.Value = slot;
            Touch();
        }

        public void PlaceCard(CardInstance card, SlotId slot)
        {
            if (card == null)
            {
                throw new ArgumentNullException("card");
            }

            EnsureBoardSlot(slot);
            if (mCardUidsBySlot[slot.Index] != 0 && mCardUidsBySlot[slot.Index] != card.Uid)
            {
                throw new InvalidOperationException("Board slot is occupied: " + slot);
            }

            ClearPreviousBoardSlot(card.Uid);
            mCardUidsBySlot[slot.Index] = card.Uid;
            card.Zone.Value = ZoneId.Board;
            card.Slot.Value = slot;
            Touch();
        }

        public int ClearSlot(SlotId slot)
        {
            EnsureBoardSlot(slot);
            var uid = mCardUidsBySlot[slot.Index];
            mCardUidsBySlot[slot.Index] = 0;
            Touch();
            return uid;
        }

        public void RemoveCard(CardInstance card)
        {
            if (card == null)
            {
                return;
            }

            ClearPreviousBoardSlot(card.Uid);
            card.Zone.Value = ZoneId.None;
            card.Slot.Value = SlotId.None;
            Touch();
        }

        public bool IsBlessed(SlotId slot)
        {
            EnsureBoardSlot(slot);
            return mBlessedSlots[slot.Index];
        }

        public void SetBlessed(SlotId slot, bool blessed)
        {
            EnsureBoardSlot(slot);
            mBlessedSlots[slot.Index] = blessed;
            Touch();
        }

        public void ClearBoardCards()
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                mCardUidsBySlot[i] = 0;
                mBlessedSlots[i] = false;
            }

            Touch();
        }

        public void Reset()
        {
            ClearBoardCards();
            AvatarUid.Value = 0;
            AvatarSlot.Value = SlotId.None;
            Touch();
        }

        private void ClearPreviousBoardSlot(int uid)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                if (mCardUidsBySlot[i] == uid)
                {
                    mCardUidsBySlot[i] = 0;
                }
            }
        }

        private void Touch()
        {
            if (Version != null)
            {
                Version.Value++;
            }
        }

        private static void EnsureBoardSlot(SlotId slot)
        {
            if (!slot.IsBoardSlot)
            {
                throw new ArgumentException("Expected board slot, got " + slot, "slot");
            }
        }
    }
}
