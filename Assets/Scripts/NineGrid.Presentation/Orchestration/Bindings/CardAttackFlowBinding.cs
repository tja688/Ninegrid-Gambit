using System;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Reactions;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class CardAttackFlowBinding : IFlowBinding
    {
        private readonly CardAttackFlow mFlow;
        private readonly DamageNumbersReaction mDamageNumbers;

        public CardAttackFlowBinding(CardAttackFlow flow, DamageNumbersReaction damageNumbers = null)
        {
            mFlow = flow;
            mDamageNumbers = damageNumbers;
        }

        public FlowId Id => FlowId.CardAttack;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            var player = registry != null ? registry.ResolveActor(payload.ActorUid) : null;
            var enemy = registry != null ? registry.ResolveActor(payload.TargetUid) : null;
            if (player == null || enemy == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            var direction = AttackDirectionResolver.Resolve(payload, registry);
            Action<string> wrappedMarker = marker =>
            {
                if (string.Equals(marker, FlowMarkers.Impact, StringComparison.Ordinal))
                {
                    CombatImpactFeedback.PlayAtImpact(registry, payload, mDamageNumbers);
                }

                onMarker?.Invoke(marker);
            };

            mFlow.Play(player, enemy, direction, wrappedMarker);
            return new FlowHandle(mFlow, wrappedMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
