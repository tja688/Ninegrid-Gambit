using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// V6：持有 Cards/Hand/Deck 显式引用，作为注册查找与 Admit/Evict 的 QF 权威入口。
    /// </summary>
    public sealed class CardEntityLifecycleSystem : AbstractSystem, ICardEntityLifecycleSystem
    {
        private CardManagerSingleton mCards;
        private CardHandManagerSingleton mHand;
        private CardDeckManagerSingleton mDeck;

        public bool IsBound => mCards != null && mHand != null && mDeck != null;

        public CardManagerSingleton Cards => mCards;

        public CardHandManagerSingleton Hand => mHand;

        public CardDeckManagerSingleton Deck => mDeck;

        public void Bind(
            CardManagerSingleton cards,
            CardHandManagerSingleton hand,
            CardDeckManagerSingleton deck)
        {
            mCards = cards;
            mHand = hand;
            mDeck = deck;
        }

        public void Unbind()
        {
            mCards = null;
            mHand = null;
            mDeck = null;
        }

        public bool TryGet(int uid, out ManagedCard card)
        {
            if (mCards != null)
            {
                return mCards.TryGet(uid, out card);
            }

            card = null;
            return false;
        }

        public void NotifyCardReleased(int uid)
        {
            mHand?.NotifyCardReleased(uid);
        }

        public HandoffState EvictFromHand(ManagedCard card)
        {
            if (mHand == null)
            {
                return HandoffState.AtRest(Vector3.zero);
            }

            return mHand.EvictCard(card);
        }

        public void AdmitToHand(ManagedCard card, in HandoffState state)
        {
            mHand?.AdmitCard(card, in state);
        }

        public HandoffState EvictFromDeck(ManagedCard card)
        {
            if (mDeck == null)
            {
                return HandoffState.AtRest(Vector3.zero);
            }

            return mDeck.EvictCard(card);
        }

        public void AdmitToDeck(ManagedCard card, in HandoffState state)
        {
            mDeck?.AdmitCard(card, in state);
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Unbind();
        }
    }
}
