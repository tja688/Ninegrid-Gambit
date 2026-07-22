using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌实体 / 手牌 / 牌库生命周期桥：Cards 不引 Presentation，由 Controller 注入显式引用。
    /// V6：跨 Manager 解析优先走 Hook，不再互取静态 Instance。
    /// </summary>
    public static class CardEntityLifecycleHook
    {
        public delegate bool TryGetManagedCardHandler(int uid, out ManagedCard card);

        /// <summary>由 Presentation Controller 注册；接收 Cards/Hand/Deck 显式绑定。</summary>
        public static Action<CardManagerSingleton, CardHandManagerSingleton, CardDeckManagerSingleton> Wire;

        public static TryGetManagedCardHandler TryGet;
        public static Action<int> NotifyCardReleased;
        public static Func<CardManagerSingleton> ResolveCards;
        public static Func<CardHandManagerSingleton> ResolveHand;
        public static Func<CardDeckManagerSingleton> ResolveDeck;

        public static void Reset()
        {
            Wire = null;
            TryGet = null;
            NotifyCardReleased = null;
            ResolveCards = null;
            ResolveHand = null;
            ResolveDeck = null;
        }

        /// <summary>生产路径：绑定三 Manager 并通知 Presentation Controller 登记 QF System。</summary>
        public static void RequestWire(
            CardManagerSingleton cards,
            CardHandManagerSingleton hand,
            CardDeckManagerSingleton deck)
        {
            ResolveCards = cards != null ? () => cards : null;
            ResolveHand = hand != null ? () => hand : null;
            ResolveDeck = deck != null ? () => deck : null;
            TryGet = cards != null
                ? (TryGetManagedCardHandler)cards.TryGet
                : null;
            NotifyCardReleased = hand != null
                ? hand.NotifyCardReleased
                : null;
            Wire?.Invoke(cards, hand, deck);
        }

        public static bool TryGetCard(int uid, out ManagedCard card)
        {
            if (TryGet != null)
            {
                return TryGet(uid, out card);
            }

            var cards = CardsOrNull();
            if (cards != null)
            {
                return cards.TryGet(uid, out card);
            }

            card = null;
            return false;
        }

        public static void RequestNotifyReleased(int uid)
        {
            if (NotifyCardReleased != null)
            {
                NotifyCardReleased(uid);
                return;
            }

            // V6 compat shell：Hook 未接线时回退 Instance。
            CardHandManagerSingleton.Instance?.NotifyCardReleased(uid);
        }

        public static CardManagerSingleton CardsOrNull()
        {
            var wired = ResolveCards?.Invoke();
            if (wired != null)
            {
                return wired;
            }

            return CardManagerSingleton.TryGetInstance();
        }

        public static CardHandManagerSingleton HandOrNull()
        {
            var wired = ResolveHand?.Invoke();
            if (wired != null)
            {
                return wired;
            }

            return CardHandManagerSingleton.Instance;
        }

        public static CardDeckManagerSingleton DeckOrNull()
        {
            var wired = ResolveDeck?.Invoke();
            if (wired != null)
            {
                return wired;
            }

            return CardDeckManagerSingleton.Instance;
        }
    }
}
