using System;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// 编辑器参数劫持：在 Plan 生成后、导演播放前，将控制台 payload 写入批次步骤与反应。
    /// </summary>
    public static class PerformanceDebugBatchHijack
    {
        public static void ApplyEditorOverrides(PresentationPlan plan, PerformanceDebugPayload payload)
        {
            if (plan == null || payload == null)
            {
                return;
            }

            bool forceDirection = payload.GetBool(PerformanceDebugPayloadKeys.ForceDirectionOverride);
            Vector2 directionOverride = forceDirection
                ? CardBattleDirectionUtil.ToVector2(payload.GetDirection(PerformanceDebugPayloadKeys.Direction))
                : Vector2.zero;

            int actorUid = payload.GetInt(PerformanceDebugPayloadKeys.ActorUid, 0);
            int targetUid = payload.GetInt(PerformanceDebugPayloadKeys.TargetUid, 0);
            int amount = payload.GetInt(PerformanceDebugPayloadKeys.Amount, -1);

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    ApplyFlowPayload(
                        steps[stepIndex]?.Payload,
                        steps[stepIndex]?.FlowId ?? FlowId.None,
                        actorUid,
                        targetUid,
                        amount,
                        forceDirection,
                        directionOverride);
                }
            }

            for (var reactionIndex = 0; reactionIndex < plan.Reactions.Count; reactionIndex++)
            {
                ApplyReactionPayload(plan.Reactions[reactionIndex]?.Payload, actorUid, targetUid, amount);
            }
        }

        private static void ApplyFlowPayload(
            FlowPayload payload,
            FlowId flowId,
            int actorUid,
            int targetUid,
            int amount,
            bool forceDirection,
            Vector2 directionOverride)
        {
            if (payload == null)
            {
                return;
            }

            if (actorUid > 0)
            {
                payload.ActorUid = actorUid;
            }

            if (targetUid > 0)
            {
                payload.TargetUid = targetUid;
                if (flowId == FlowId.CardKill)
                {
                    payload.CardUid = targetUid;
                }
            }

            if (amount >= 0)
            {
                payload.Amount = amount;
            }

            if (forceDirection
                && directionOverride.sqrMagnitude > 0.0001f
                && (flowId == FlowId.CardAttack || flowId == FlowId.CardKill))
            {
                payload.Direction = directionOverride;
            }
        }

        private static void ApplyReactionPayload(
            FlowPayload payload,
            int actorUid,
            int targetUid,
            int amount)
        {
            if (payload == null)
            {
                return;
            }

            if (actorUid > 0)
            {
                payload.ActorUid = actorUid;
            }

            if (targetUid > 0)
            {
                payload.TargetUid = targetUid;
            }

            if (amount >= 0)
            {
                payload.Amount = amount;
            }
        }
    }
}
