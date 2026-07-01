using NineGrid.Core;
using NineGrid.Presentation.Shared;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// 将控制台 payload 同步到场景演员布局（槽位优先，direction 推导邻格目标）。
    /// </summary>
    public static class PerformanceDebugLayoutApplier
    {
        public static void Apply(
            PerformanceDebugHarness harness,
            PerformanceDebugContextPreset effectivePreset,
            PerformanceDebugPayload payload)
        {
            if (harness == null || payload == null)
            {
                return;
            }

            if (effectivePreset != PerformanceDebugContextPreset.BattlePair
                && effectivePreset != PerformanceDebugContextPreset.Board9)
            {
                return;
            }

            int playerSlot = payload.GetBoardSlot(PerformanceDebugPayloadKeys.PlayerSlot, 5);
            int targetSlot = ResolveTargetSlot(payload, playerSlot);
            int actorUid = payload.GetInt(PerformanceDebugPayloadKeys.ActorUid, PerformanceDebugActorUids.Player);
            int targetUid = payload.GetInt(PerformanceDebugPayloadKeys.TargetUid, PerformanceDebugActorUids.Enemy);
            harness.ApplyBattleSlots(playerSlot, targetSlot, actorUid, targetUid);
        }

        public static int ResolveTargetSlot(PerformanceDebugPayload payload, int playerSlot)
        {
            string rawTargetSlot = payload.GetString(PerformanceDebugPayloadKeys.TargetSlot);
            if (!string.IsNullOrEmpty(rawTargetSlot)
                && !rawTargetSlot.Equals("Auto", System.StringComparison.OrdinalIgnoreCase)
                && int.TryParse(rawTargetSlot, out int explicitSlot)
                && explicitSlot >= SlotId.MinBoardIndex
                && explicitSlot <= SlotId.MaxBoardIndex)
            {
                return explicitSlot;
            }

            CardBattleDirection direction = payload.GetDirection(PerformanceDebugPayloadKeys.Direction);
            if (CardBattleDirectionUtil.TryGetNeighborForDirection(SlotId.Board(playerSlot), direction, out SlotId neighbor))
            {
                return neighbor.Index;
            }

            return PerformanceDebugActorUids.EnemySlot.Index;
        }

        public static CardBattleDirection ResolveDerivedDirection(int playerSlot, int targetSlot)
        {
            if (CardBattleDirectionUtil.TryFromBoardSlots(
                    SlotId.Board(playerSlot),
                    SlotId.Board(targetSlot),
                    out UnityEngine.Vector2 direction))
            {
                if (direction == UnityEngine.Vector2.up)
                {
                    return CardBattleDirection.Up;
                }

                if (direction == UnityEngine.Vector2.left)
                {
                    return CardBattleDirection.Left;
                }

                if (direction == UnityEngine.Vector2.down)
                {
                    return CardBattleDirection.Down;
                }
            }

            return CardBattleDirection.Right;
        }

        public static string FormatDerivedDirection(PerformanceDebugPayload payload)
        {
            if (payload == null)
            {
                return CardBattleDirection.Right.ToString();
            }

            int playerSlot = payload.GetBoardSlot(PerformanceDebugPayloadKeys.PlayerSlot, 5);
            int targetSlot = ResolveTargetSlot(payload, playerSlot);
            return ResolveDerivedDirection(playerSlot, targetSlot).ToString();
        }

        public static void SyncDerivedDirection(PerformanceDebugPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            payload.Set("derivedDirection", FormatDerivedDirection(payload));
        }
    }
}
