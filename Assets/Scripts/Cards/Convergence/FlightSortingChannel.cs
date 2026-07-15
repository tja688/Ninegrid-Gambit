using System;
using System.Collections.Generic;
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
        /// <summary>全局飞行层：远高于场地默认序（GroundCardSortingOrder = -10）。</summary>
        public const int GlobalFlightSortingOrder = 80;

        private static readonly Dictionary<int, ActiveFlight> ActiveByUid = new();

        private sealed class ActiveFlight
        {
            public LayerConvergenceDriver Driver;
            public Action Handler;
            public int TargetOrder;
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
            var targetOrder = ResolveTargetOrder(card);
            RaiseSortingOrder(card, ResolveFlightOrder(card));

            if (ActiveByUid.TryGetValue(uid, out var existing))
            {
                if (existing.Driver != null && existing.Handler != null)
                {
                    existing.Driver.Completed -= existing.Handler;
                }

                existing.Driver = driver;
                existing.TargetOrder = targetOrder;
                existing.Handler = () => OnDriverCompleted(uid);
                driver.Completed += existing.Handler;
                return;
            }

            Action handler = () => OnDriverCompleted(uid);
            ActiveByUid[uid] = new ActiveFlight
            {
                Driver = driver,
                Handler = handler,
                TargetOrder = targetOrder,
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

        /// <summary>离散掉回目标序。</summary>
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
                ApplySortingOrder(card, active.TargetOrder);
                return;
            }

            ApplySortingOrder(card, ResolveTargetOrder(card));
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

            if (CardManagerSingleton.Instance == null
                || !CardManagerSingleton.Instance.TryGet(uid, out var card)
                || card == null)
            {
                return;
            }

            ApplySortingOrder(card, active.TargetOrder);
        }

        private static void RaiseSortingOrder(ManagedCard card, int order) =>
            ApplySortingOrder(card, order);

        private static void ApplySortingOrder(ManagedCard card, int order)
        {
            if (card?.View == null)
            {
                return;
            }

            var group = card.View.GetComponent<SortingGroup>();
            if (group != null)
            {
                group.sortingOrder = order;
            }
        }
    }
}
