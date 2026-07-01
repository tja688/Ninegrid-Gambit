using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    public static class PerformanceDebugFlowPayloadBuilder
    {
        public static FlowPayload BuildBattleFlowPayload(PerformanceDebugPayload payload)
        {
            int playerSlot = payload.GetBoardSlot(PerformanceDebugPayloadKeys.PlayerSlot, 5);
            int targetSlot = PerformanceDebugLayoutApplier.ResolveTargetSlot(payload, playerSlot);
            var flowPayload = new FlowPayload
            {
                ActorUid = payload.GetInt(PerformanceDebugPayloadKeys.ActorUid, PerformanceDebugActorUids.Player),
                TargetUid = payload.GetInt(PerformanceDebugPayloadKeys.TargetUid, PerformanceDebugActorUids.Enemy),
                CardUid = payload.GetInt(PerformanceDebugPayloadKeys.TargetUid, PerformanceDebugActorUids.Enemy),
                Amount = payload.GetInt(PerformanceDebugPayloadKeys.Amount, 3),
            };

            if (payload.GetBool(PerformanceDebugPayloadKeys.ForceDirectionOverride))
            {
                flowPayload.Direction = CardBattleDirectionUtil.ToVector2(
                    payload.GetDirection(PerformanceDebugPayloadKeys.Direction));
            }

            return flowPayload;
        }
    }
}
