using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    /// <summary>
    /// 编辑器参数劫持：在 Plan 生成后、导演播放前，将控制台 payload 写入批次步骤。
    /// 生产管线仍由 <see cref="AttackDirectionResolver"/> 推导缺省方向；调试层单一覆写权威。
    /// </summary>
    public static class PerformanceDebugBatchHijack
    {
        public static void ApplyEditorOverrides(PresentationPlan plan, PerformanceDebugPayload payload)
        {
            if (plan == null || payload == null)
            {
                return;
            }

            Vector2 direction = CardBattleDirectionUtil.ToVector2(payload.GetDirection("direction"));
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    PlanStep step = steps[stepIndex];
                    if (step?.Payload == null)
                    {
                        continue;
                    }

                    if (step.FlowId == FlowId.CardAttack || step.FlowId == FlowId.CardKill)
                    {
                        step.Payload.Direction = direction;
                    }
                }
            }
        }
    }
}
