using System;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class CounterattackFlowBinding : IFlowBinding
    {
        private readonly CounterattackFlow mFlow;
        private readonly DamageNumberFeedback mDamageNumbers;

        public CounterattackFlowBinding(CounterattackFlow flow, DamageNumberFeedback damageNumbers = null)
        {
            mFlow = flow;
            mDamageNumbers = damageNumbers;
        }

        public FlowId Id => FlowId.Counterattack;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            var attacker = registry != null ? registry.ResolveActor(payload.ActorUid) : null;
            var target = registry != null ? registry.ResolveActor(payload.TargetUid) : null;
            if (attacker == null || target == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            Vector2 boardDirection = AttackDirectionResolver.Resolve(payload, registry);
            Action<string> wrappedMarker = marker =>
            {
                if (string.Equals(marker, FlowMarkers.Impact, StringComparison.Ordinal))
                {
                    CombatImpactFeedback.PlayAtImpact(registry, payload, mDamageNumbers);
                }

                onMarker?.Invoke(marker);
            };

            mFlow.Play(attacker, target, boardDirection, wrappedMarker);
            return new FlowHandle(mFlow, wrappedMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    public sealed class CounterattackKillFlowBinding : IFlowBinding
    {
        private readonly CounterattackKillFlow mFlow;

        public CounterattackKillFlowBinding(CounterattackKillFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.CounterattackKill;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            int attackerUid = payload.ActorUid > 0 ? payload.ActorUid : PresentationFallbackActorUids.Enemy;
            int targetUid = payload.CardUid > 0
                ? payload.CardUid
                : payload.TargetUid > 0
                    ? payload.TargetUid
                    : PresentationFallbackActorUids.Player;
            var attacker = registry?.ResolveActor(attackerUid);
            var target = registry?.ResolveActor(targetUid);
            if (attacker == null || target == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            Vector2 boardDirection = AttackDirectionResolver.Resolve(payload, registry);
            bool includeStrike = payload?.IncludeStrike ?? false;
            mFlow.Play(attacker, target, boardDirection, includeStrike);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
