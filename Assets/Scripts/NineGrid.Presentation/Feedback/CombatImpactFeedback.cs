using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Feedback
{
    /// <summary>
    /// 战斗 Impact 时刻的本地反馈：伤害飘字 + 卡面 HP/护甲跳变。
    /// </summary>
    public static class CombatImpactFeedback
    {
        public static void PlayAtImpact(
            IViewRegistry registry,
            FlowPayload payload,
            DamageNumberFeedback damageNumbers)
        {
            if (payload == null)
            {
                return;
            }

            int targetUid = payload.TargetUid > 0 ? payload.TargetUid : payload.CardUid;
            Transform target = registry?.ResolveActor(targetUid);
            if (target == null)
            {
                return;
            }

            if (damageNumbers != null && payload.Amount > 0)
            {
                damageNumbers.Play(target, payload.Amount, DamagePopupKind.Damage);
            }

            ApplyCardStatusAtImpact(target, payload);
        }

        private static void ApplyCardStatusAtImpact(Transform target, FlowPayload payload)
        {
            TableNineCardStatusView view = target.GetComponentInChildren<TableNineCardStatusView>(true);
            if (view == null)
            {
                return;
            }

            if (payload.RemainingHp >= 0)
            {
                view.PlayLifeTo(payload.RemainingHp, animate: true);
            }

            if (payload.RemainingArmor >= 0)
            {
                view.PlayArmorTo(payload.RemainingArmor, animate: true);
            }
        }
    }
}
