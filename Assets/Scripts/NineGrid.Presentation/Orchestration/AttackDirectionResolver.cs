using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 从棋盘槽位推导正交攻击方向；批次事件未显式注入 Direction 时使用。
    /// </summary>
    public static class AttackDirectionResolver
    {
        public static void ApplyBoardDirection(FlowPayload payload, CoreViewSnapshot snapshot)
        {
            if (payload == null || payload.Direction.sqrMagnitude > 0.0001f)
            {
                return;
            }

            if (TryResolveBoardDirection(payload, snapshot, out Vector2 direction))
            {
                payload.Direction = direction;
            }
        }

        public static Vector2 Resolve(FlowPayload payload, IViewRegistry registry, CoreViewSnapshot snapshot = null)
        {
            if (payload != null && payload.Direction.sqrMagnitude > 0.0001f)
            {
                return payload.Direction;
            }

            if (TryResolveBoardDirection(payload, snapshot, registry, out Vector2 direction))
            {
                return direction;
            }

            return Vector2.right;
        }

        private static bool TryResolveBoardDirection(
            FlowPayload payload,
            CoreViewSnapshot snapshot,
            out Vector2 direction)
        {
            return TryResolveBoardDirection(payload, snapshot, null, out direction);
        }

        private static bool TryResolveBoardDirection(
            FlowPayload payload,
            CoreViewSnapshot snapshot,
            IViewRegistry registry,
            out Vector2 direction)
        {
            direction = Vector2.zero;
            if (payload == null)
            {
                return false;
            }

            SlotId fromSlot = ResolveActorSlot(payload.ActorUid, snapshot, registry, PerformanceDebugActorUids.PlayerSlot);
            SlotId toSlot = ResolveTargetSlot(payload, snapshot, registry);
            if (fromSlot.IsNone || toSlot.IsNone)
            {
                return false;
            }

            return CardBattleDirectionUtil.TryFromBoardSlots(fromSlot, toSlot, out direction);
        }

        private static SlotId ResolveTargetSlot(
            FlowPayload payload,
            CoreViewSnapshot snapshot,
            IViewRegistry registry)
        {
            if (payload.ToSlot.IsBoardSlot)
            {
                return payload.ToSlot;
            }

            int targetUid = payload.TargetUid > 0 ? payload.TargetUid : payload.CardUid;
            SlotId slot = ResolveActorSlot(targetUid, snapshot, registry, SlotId.None);
            if (!slot.IsNone)
            {
                return slot;
            }

            if (targetUid == PerformanceDebugActorUids.Enemy)
            {
                return PerformanceDebugActorUids.EnemySlot;
            }

            return SlotId.None;
        }

        private static SlotId ResolveActorSlot(
            int cardUid,
            CoreViewSnapshot snapshot,
            IViewRegistry registry,
            SlotId fallback)
        {
            if (cardUid > 0 && snapshot != null && snapshot.TryGetCard(cardUid, out CardView card) && card.Slot.IsBoardSlot)
            {
                return card.Slot;
            }

            if (cardUid == PerformanceDebugActorUids.Player)
            {
                return PerformanceDebugActorUids.PlayerSlot;
            }

            if (cardUid == PerformanceDebugActorUids.Enemy)
            {
                return PerformanceDebugActorUids.EnemySlot;
            }

            if (cardUid >= PerformanceDebugActorUids.BoardCard(1)
                && cardUid <= PerformanceDebugActorUids.BoardCard(9))
            {
                return SlotId.Board(cardUid - 100);
            }

            return fallback;
        }
    }
}
