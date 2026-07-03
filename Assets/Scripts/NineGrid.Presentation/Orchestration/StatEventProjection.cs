using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 批内 Stat 事件直连投影：刷新卡面/化身数值，不承担 spawn/despawn/落位。
    /// 伤害路径的 HP/护甲跳变由 <see cref="Feedback.CombatImpactFeedback"/> 在 Impact 承担。
    /// </summary>
    public sealed class StatEventProjection
    {
        private readonly IViewRegistry mViewRegistry;
        private readonly TableNineStatusPanelView mStatusPanel;

        public StatEventProjection(IViewRegistry viewRegistry, TableNineStatusPanelView statusPanel = null)
        {
            mViewRegistry = viewRegistry;
            mStatusPanel = statusPanel;
        }

        public void Apply(IReadOnlyList<PresentationInstruction> instructions, CoreViewSnapshot snapshot)
        {
            if (instructions == null || instructions.Count == 0 || snapshot == null)
            {
                return;
            }

            var attackImpactTargets = CollectAttackImpactTargets(instructions);
            for (var i = 0; i < instructions.Count; i++)
            {
                PresentationInstruction instruction = instructions[i];
                CoreGameEvent evt = instruction?.Event;
                if (evt == null || !IsStatEvent(evt.Type))
                {
                    continue;
                }

                if (ShouldSkipForAttackImpact(evt, attackImpactTargets))
                {
                    continue;
                }

                ApplyStatEvent(evt, snapshot);
            }
        }

        private void ApplyStatEvent(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (IsAvatarEvent(evt, snapshot))
            {
                mStatusPanel?.ApplyEvent(evt, snapshot);
                return;
            }

            int cardUid = evt.CardUid > 0 ? evt.CardUid : evt.TargetUid;
            if (cardUid <= 0)
            {
                return;
            }

            Transform actor = mViewRegistry?.ResolveActor(cardUid);
            if (actor == null)
            {
                return;
            }

            TableNineCardStatusView view = actor.GetComponentInChildren<TableNineCardStatusView>(true);
            view?.ApplyEvent(evt, snapshot, animate: false);
        }

        public static bool IsStatEvent(CoreEventType type)
        {
            return type == CoreEventType.HpChanged
                || type == CoreEventType.Healed
                || type == CoreEventType.ArmorChanged
                || type == CoreEventType.BaseStatModified;
        }

        public static bool ShouldSkipForAttackImpact(
            CoreGameEvent evt,
            HashSet<(int ActionId, int CardUid)> attackImpactTargets)
        {
            if (attackImpactTargets == null || attackImpactTargets.Count == 0)
            {
                return false;
            }

            if (evt.Type != CoreEventType.HpChanged && evt.Type != CoreEventType.ArmorChanged)
            {
                return false;
            }

            int cardUid = evt.CardUid > 0 ? evt.CardUid : evt.TargetUid;
            if (cardUid <= 0)
            {
                return false;
            }

            return attackImpactTargets.Contains((evt.ActionId, cardUid));
        }

        private static HashSet<(int ActionId, int CardUid)> CollectAttackImpactTargets(
            IReadOnlyList<PresentationInstruction> instructions)
        {
            var targets = new HashSet<(int, int)>();
            for (var i = 0; i < instructions.Count; i++)
            {
                CoreGameEvent evt = instructions[i]?.Event;
                if (evt == null || evt.Type != CoreEventType.DamageDealt)
                {
                    continue;
                }

                if (!InstructionKindFlowRouter.ShouldSynthesizeAttackFlow(evt))
                {
                    continue;
                }

                int cardUid = evt.CardUid > 0 ? evt.CardUid : evt.TargetUid;
                if (cardUid > 0)
                {
                    targets.Add((evt.ActionId, cardUid));
                }
            }

            return targets;
        }

        private static bool IsAvatarEvent(CoreGameEvent evt, CoreViewSnapshot snapshot)
        {
            if (snapshot == null || evt == null)
            {
                return false;
            }

            int cardUid = evt.CardUid > 0 ? evt.CardUid : evt.TargetUid;
            return cardUid > 0 && cardUid == snapshot.AvatarUid;
        }
    }
}
