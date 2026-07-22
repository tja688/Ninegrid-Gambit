using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 卡牌实体 / 手牌 / 牌库生命周期 Controller：将场景 Manager 登记到 QF System，并接线 Hook。
    /// </summary>
    public sealed class CardEntityLifecycleController : PresentationController
    {
        private CardManagerSingleton mCards;
        private CardHandManagerSingleton mHand;
        private CardDeckManagerSingleton mDeck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            CardEntityLifecycleHook.Wire = WireManagers;
        }

        protected override void OnBind()
        {
            if (mCards != null || mHand != null || mDeck != null)
            {
                ApplyBind(mCards, mHand, mDeck);
            }
        }

        protected override void OnUnbind()
        {
            ClearBind();
        }

        /// <summary>绑定三 Manager（Hook 与 EditMode 直驱共用）。</summary>
        public void BindManagers(
            CardManagerSingleton cards,
            CardHandManagerSingleton hand,
            CardDeckManagerSingleton deck)
        {
            mCards = cards;
            mHand = hand;
            mDeck = deck;
            ApplyBind(cards, hand, deck);
        }

        private void ClearBind()
        {
            var system = TryGetLifecycleSystem();
            if (system != null
                && ReferenceEquals(system.Cards, mCards)
                && ReferenceEquals(system.Hand, mHand)
                && ReferenceEquals(system.Deck, mDeck))
            {
                system.Unbind();
            }

            if (ReferenceEquals(CardEntityLifecycleHook.ResolveCards?.Invoke(), mCards))
            {
                CardEntityLifecycleHook.ResolveCards = null;
                CardEntityLifecycleHook.TryGet = null;
            }

            if (ReferenceEquals(CardEntityLifecycleHook.ResolveHand?.Invoke(), mHand))
            {
                CardEntityLifecycleHook.ResolveHand = null;
                CardEntityLifecycleHook.NotifyCardReleased = null;
            }

            if (ReferenceEquals(CardEntityLifecycleHook.ResolveDeck?.Invoke(), mDeck))
            {
                CardEntityLifecycleHook.ResolveDeck = null;
            }

            mCards = null;
            mHand = null;
            mDeck = null;
        }

        private static void WireManagers(
            CardManagerSingleton cards,
            CardHandManagerSingleton hand,
            CardDeckManagerSingleton deck)
        {
            var existing = Object.FindObjectOfType<CardEntityLifecycleController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(CardEntityLifecycleController));
                existing = host.AddComponent<CardEntityLifecycleController>();
            }

            existing.BindManagers(cards, hand, deck);
        }

        private static void ApplyBind(
            CardManagerSingleton cards,
            CardHandManagerSingleton hand,
            CardDeckManagerSingleton deck)
        {
            var system = EnsureLifecycleSystem();
            system.Bind(cards, hand, deck);

            CardEntityLifecycleHook.ResolveCards = cards != null ? () => cards : null;
            CardEntityLifecycleHook.ResolveHand = hand != null ? () => hand : null;
            CardEntityLifecycleHook.ResolveDeck = deck != null ? () => deck : null;
            CardEntityLifecycleHook.TryGet = cards != null
                ? (CardEntityLifecycleHook.TryGetManagedCardHandler)cards.TryGet
                : null;
            CardEntityLifecycleHook.NotifyCardReleased = hand != null
                ? hand.NotifyCardReleased
                : null;
        }

        private static ICardEntityLifecycleSystem EnsureLifecycleSystem()
        {
            var architecture = NineGridArchitecture.Interface;
            var existing = architecture.GetSystem<ICardEntityLifecycleSystem>();
            if (existing != null)
            {
                return existing;
            }

            var created = new CardEntityLifecycleSystem();
            architecture.RegisterSystem<ICardEntityLifecycleSystem>(created);
            return created;
        }

        private static ICardEntityLifecycleSystem TryGetLifecycleSystem()
        {
            var architecture = NineGridArchitecture.Interface;
            return architecture?.GetSystem<ICardEntityLifecycleSystem>();
        }
    }
}
