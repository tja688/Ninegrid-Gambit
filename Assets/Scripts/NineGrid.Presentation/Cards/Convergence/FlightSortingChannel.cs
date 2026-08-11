using System;
using System.Collections.Generic;
using NineGrid.Cards.Anim;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 排序离散事件通道（方案 A）：不进收敛体系。
    /// 收敛开始抬到全局飞行层；L2 完成事件触发时掉回目标 sortingOrder。
    /// 跳变挂靠在已就位瞬间，肉眼几乎不可察。
    /// </summary>
    public static class FlightSortingChannel
    {
        private const string MainSortingLayerName = "Main";

        /// <summary>全局飞行层：远高于场地默认序（GroundCardSortingOrder = -10）。</summary>
        public const int GlobalFlightSortingOrder = 80;

        private static readonly Dictionary<int, ActiveFlight> ActiveByUid = new();

        private sealed class ActiveFlight
        {
            public LayerConvergenceDriver Driver;
            public Action Handler;
        }

        public static int ResolveFlightOrder(ManagedCard card)
        {
            var micro = card != null ? Mathf.Abs(card.Uid) % 16 : 0;
            return GlobalFlightSortingOrder + micro;
        }

        public static int ResolveTargetOrder(ManagedCard card)
        {
            if (card == null)
            {
                return CardDisplayModeVisuals.GroundCardSortingOrder;
            }

            // 净土域自有槽位序（左高右低）；不可回落到 DisplayMode 默认值，否则会吞掉手牌/卡组规则。
            if (card.DisplayMode == CardDisplayMode.HandCardMode)
            {
                var hand = CardEntityLifecycleHook.HandOrNull();
                if (hand != null && hand.TryResolveSortingOrder(card, out var handOrder))
                {
                    return handOrder;
                }
            }
            else if (card.DisplayMode == CardDisplayMode.CardDeckMode)
            {
                var deck = CardEntityLifecycleHook.DeckOrNull();
                if (deck != null && deck.TryResolveSortingOrder(card, out var deckOrder))
                {
                    return deckOrder;
                }
            }

            return CardDisplayModeVisuals.GetSortingOrder(card.DisplayMode, card.CoreKind);
        }

        /// <summary>
        /// 挂靠 L2 收敛：开始抬飞行层，完成时掉回目标序。Redirect 重入时保持抬升、只换完成回调。
        /// </summary>
        public static void ArmForSlotConvergence(ManagedCard card, LayerConvergenceDriver driver)
        {
            if (card == null || driver == null || card.Uid <= 0)
            {
                return;
            }

            var uid = card.Uid;
            RaiseSortingOrder(card, ResolveFlightOrder(card));

            if (ActiveByUid.TryGetValue(uid, out var existing))
            {
                if (existing.Driver != null && existing.Handler != null)
                {
                    existing.Driver.Completed -= existing.Handler;
                }

                existing.Driver = driver;
                existing.Handler = () => OnDriverCompleted(uid);
                driver.Completed += existing.Handler;
                return;
            }

            Action handler = () => OnDriverCompleted(uid);
            ActiveByUid[uid] = new ActiveFlight
            {
                Driver = driver,
                Handler = handler,
            };
            driver.Completed += handler;
        }

        /// <summary>离散抬升（融合等不绑 L2 完成事件的场景）。</summary>
        public static void Raise(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            RaiseSortingOrder(card, ResolveFlightOrder(card));
        }

        /// <summary>离散掉回目标序（完成瞬间按当前域规则重解析，不沿用起飞时冻结值）。</summary>
        public static void Restore(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            if (ActiveByUid.TryGetValue(card.Uid, out var active))
            {
                if (active.Driver != null && active.Handler != null)
                {
                    active.Driver.Completed -= active.Handler;
                }

                ActiveByUid.Remove(card.Uid);
            }

            ApplyDomainSorting(card);
        }

        /// <summary>EditMode / 取消路径：清掉 uid 上的挂靠，不改 sorting。</summary>
        public static void Disarm(int uid)
        {
            if (uid <= 0 || !ActiveByUid.TryGetValue(uid, out var active))
            {
                return;
            }

            if (active.Driver != null && active.Handler != null)
            {
                active.Driver.Completed -= active.Handler;
            }

            ActiveByUid.Remove(uid);
        }

        public static bool IsArmed(int uid) => uid > 0 && ActiveByUid.ContainsKey(uid);

        private static void OnDriverCompleted(int uid)
        {
            if (!ActiveByUid.TryGetValue(uid, out var active))
            {
                return;
            }

            ActiveByUid.Remove(uid);
            if (active.Driver != null && active.Handler != null)
            {
                active.Driver.Completed -= active.Handler;
            }

            if (CardEntityLifecycleHook.CardsOrNull() == null
                || !CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var card)
                || card == null)
            {
                return;
            }

            // 掉回时按就位瞬间的域规则重解析（手牌/卡组槽位序 + Propagate），
            // 避免起飞时冻结的 DisplayMode 默认值覆盖净土域规则。
            ApplyDomainSorting(card);
        }

        private static void RaiseSortingOrder(ManagedCard card, int order) =>
            ApplySortingOrder(card, order);

        /// <summary>
        /// 手牌/卡组已入槽时委托域权威 ApplySortingOrder（含 layer Propagate）；
        /// 否则只写 SG order。
        /// </summary>
        private static void ApplyDomainSorting(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            if (card.DisplayMode == CardDisplayMode.HandCardMode)
            {
                var hand = CardEntityLifecycleHook.HandOrNull();
                if (hand != null && hand.ContainsUid(card.Uid))
                {
                    hand.EnsureHandSorting(card);
                    return;
                }
            }
            else if (card.DisplayMode == CardDisplayMode.CardDeckMode)
            {
                var deck = CardEntityLifecycleHook.DeckOrNull();
                if (deck != null && deck.ContainsUid(card.Uid))
                {
                    deck.EnsureDeckSorting(card);
                    return;
                }
            }

            ApplySortingOrder(card, ResolveTargetOrder(card));
        }

        private static void ApplySortingOrder(ManagedCard card, int order)
        {
            if (card?.View == null)
            {
                return;
            }

            var group = card.View.GetComponent<SortingGroup>();
            if (group == null)
            {
                return;
            }

            if (card.DisplayMode != CardDisplayMode.CardDeckMode
                && group.sortingLayerName != MainSortingLayerName)
            {
                group.sortingLayerName = MainSortingLayerName;
                CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(group);
            }

            group.sortingOrder = order;
        }
    }
}
