using System;
using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core
{
    public sealed class CardRegistry : AbstractModel
    {
        private readonly Dictionary<int, CardInstance> mCards = new Dictionary<int, CardInstance>();
        private int mNextUid = 1;

        public BindableProperty<int> Version { get; private set; }

        public IReadOnlyDictionary<int, CardInstance> Cards
        {
            get { return mCards; }
        }

        protected override void OnInit()
        {
            if (Version == null)
            {
                Version = new BindableProperty<int>(0);
            }
        }

        public CardInstance Create(string defId, CardKind kind)
        {
            var card = new CardInstance(mNextUid++, defId, kind);
            mCards.Add(card.Uid, card);
            Touch();
            return card;
        }

        public CardInstance Get(int uid)
        {
            CardInstance card;
            if (!mCards.TryGetValue(uid, out card))
            {
                throw new KeyNotFoundException("Card uid not found: " + uid);
            }

            return card;
        }

        public bool TryGet(int uid, out CardInstance card)
        {
            return mCards.TryGetValue(uid, out card);
        }

        public void MoveCard(int uid, ZoneId zone, SlotId slot)
        {
            var card = Get(uid);
            card.Zone.Value = zone;
            card.Slot.Value = slot;
            Touch();
        }

        public bool Remove(int uid)
        {
            var removed = mCards.Remove(uid);
            if (removed)
            {
                Touch();
            }

            return removed;
        }

        public void Clear()
        {
            mCards.Clear();
            mNextUid = 1;
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
