using System;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class CardAttackFlowBinding : IFlowBinding
    {
        private readonly CardAttackFlow mFlow;

        public CardAttackFlowBinding(CardAttackFlow flow)
        {
            mFlow = flow;
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

            var direction = payload.Direction.sqrMagnitude > 0.0001f ? payload.Direction : Vector2.right;
            mFlow.Play(player, enemy, direction, onMarker);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
