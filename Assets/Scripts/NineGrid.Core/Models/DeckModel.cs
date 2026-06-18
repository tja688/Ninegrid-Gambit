using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core
{
    public sealed class DeckModel : AbstractModel
    {
        private readonly List<int> mDrawPileUids = new List<int>();
        private readonly List<int> mPlayerCardPoolUids = new List<int>();
        private readonly List<int> mEnemyCardPoolUids = new List<int>();
        private readonly List<int> mItemSlotUids = new List<int>();

        public BindableProperty<int> Version { get; private set; }

        public IReadOnlyList<int> DrawPileUids
        {
            get { return mDrawPileUids; }
        }

        public IReadOnlyList<int> PlayerCardPoolUids
        {
            get { return mPlayerCardPoolUids; }
        }

        public IReadOnlyList<int> EnemyCardPoolUids
        {
            get { return mEnemyCardPoolUids; }
        }

        public IReadOnlyList<int> ItemSlotUids
        {
            get { return mItemSlotUids; }
        }

        protected override void OnInit()
        {
            if (Version == null)
            {
                Version = new BindableProperty<int>(0);
            }
        }

        public void AddToDrawPile(CardInstance card, bool top)
        {
            RemoveCard(card);
            if (top)
            {
                mDrawPileUids.Insert(0, card.Uid);
            }
            else
            {
                mDrawPileUids.Add(card.Uid);
            }

            card.Zone.Value = ZoneId.DrawPile;
            card.Slot.Value = SlotId.None;
            Touch();
        }

        public void AddToPlayerCardPool(CardInstance card)
        {
            RemoveCard(card);
            mPlayerCardPoolUids.Add(card.Uid);
            card.Zone.Value = ZoneId.PlayerCardPool;
            card.Slot.Value = SlotId.None;
            Touch();
        }

        public void AddToEnemyCardPool(CardInstance card)
        {
            RemoveCard(card);
            mEnemyCardPoolUids.Add(card.Uid);
            card.Zone.Value = ZoneId.EnemyCardPool;
            card.Slot.Value = SlotId.None;
            Touch();
        }

        public void AddToItemSlots(CardInstance card)
        {
            RemoveCard(card);
            mItemSlotUids.Add(card.Uid);
            card.Zone.Value = ZoneId.ItemSlots;
            card.Slot.Value = SlotId.None;
            Touch();
        }

        public bool RemoveCard(CardInstance card)
        {
            if (card == null)
            {
                return false;
            }

            var removed = RemoveUid(card.Uid);
            if (removed)
            {
                card.Zone.Value = ZoneId.None;
                card.Slot.Value = SlotId.None;
                Touch();
            }

            return removed;
        }

        public bool RemoveUid(int uid)
        {
            var removed = mDrawPileUids.Remove(uid);
            removed = mPlayerCardPoolUids.Remove(uid) || removed;
            removed = mEnemyCardPoolUids.Remove(uid) || removed;
            removed = mItemSlotUids.Remove(uid) || removed;
            if (removed)
            {
                Touch();
            }

            return removed;
        }

        public void Clear()
        {
            mDrawPileUids.Clear();
            mPlayerCardPoolUids.Clear();
            mEnemyCardPoolUids.Clear();
            mItemSlotUids.Clear();
            Touch();
        }

        private void Touch()
        {
            if (Version != null)
            {
                Version.Value++;
            }
        }
    }
}
