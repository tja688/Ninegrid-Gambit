using System;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class CounterattackFlowBinding : IFlowBinding
    {
        private readonly CounterattackFlow mFlow;

        public CounterattackFlowBinding(CounterattackFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.Counterattack;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            int playerUid = payload.ActorUid > 0 ? payload.ActorUid : Debugging.PerformanceDebugActorUids.Player;
            int enemyUid = payload.TargetUid > 0 ? payload.TargetUid : Debugging.PerformanceDebugActorUids.Enemy;
            var player = registry?.ResolveActor(playerUid);
            var enemy = registry?.ResolveActor(enemyUid);
            if (player == null || enemy == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            Vector2 boardDirection = AttackDirectionResolver.Resolve(payload, registry);
            mFlow.Play(enemy, player, boardDirection);
            return new FlowHandle(mFlow, onMarker);
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

            int playerUid = payload.ActorUid > 0 ? payload.ActorUid : Debugging.PerformanceDebugActorUids.Player;
            int enemyUid = payload.CardUid > 0
                ? payload.CardUid
                : payload.TargetUid > 0
                    ? payload.TargetUid
                    : Debugging.PerformanceDebugActorUids.Enemy;
            var player = registry?.ResolveActor(playerUid);
            var enemy = registry?.ResolveActor(enemyUid);
            if (player == null || enemy == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            Vector2 boardDirection = AttackDirectionResolver.Resolve(payload, registry);
            mFlow.Play(enemy, player, boardDirection);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
