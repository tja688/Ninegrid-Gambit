using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Debugging.Trace;
using NineGrid.Presentation.Flow.Hand;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class CardAcquisitionFlowBinding : IFlowBinding
    {
        private readonly CardAcquisitionFlow mFlow;
        private readonly HandLayoutPresenter mLayoutPresenter;
        private readonly HandCardLayoutSolver mLayoutSolver;
        private readonly Transform mHandActorsRoot;
        private readonly HandItemsPresenter mHandItemsPresenter;

        public CardAcquisitionFlowBinding(
            CardAcquisitionFlow flow,
            HandLayoutPresenter layoutPresenter,
            HandCardLayoutSolver layoutSolver,
            Transform handActorsRoot,
            HandItemsPresenter handItemsPresenter = null)
        {
            mFlow = flow;
            mLayoutPresenter = layoutPresenter;
            mLayoutSolver = layoutSolver;
            mHandActorsRoot = handActorsRoot;
            mHandItemsPresenter = handItemsPresenter;
        }

        public FlowId Id => FlowId.CardAcquisition;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null || registry == null || payload == null || payload.CardUid <= 0)
            {
                BattleTraceHooks.RecordFlowResolve(Id, "invalid_payload", "missing flow/registry/payload/cardUid", payload);
                return new FlowHandle(mFlow, onMarker);
            }

            Transform acquired = registry.ResolveActor(payload.CardUid);
            FlowPlaybackScope scope = FlowPlaybackScope.Current;
            if (acquired == null && scope?.ActorFactory != null && scope.Snapshot != null
                && scope.Snapshot.TryGetCard(payload.CardUid, out CardView cardView))
            {
                acquired = scope.ActorFactory.Spawn(cardView.DefId, payload.CardUid, mHandActorsRoot);
                registry.RegisterActor(payload.CardUid, acquired);
            }

            if (acquired != null)
            {
                HandInteractionActorWiring.EnsureHandRelay(acquired, payload.CardUid);
                mHandItemsPresenter?.RefreshHandActors(scope?.Snapshot);
            }

            if (acquired == null || mLayoutPresenter == null || mLayoutSolver == null)
            {
                BattleTraceHooks.RecordFlowResolve(
                    Id,
                    acquired == null ? "missing_actor" : "missing_layout",
                    acquired == null ? "ResolveActor/spawn failed" : "Hand layout presenter/solver missing",
                    payload);
                return new FlowHandle(mFlow, onMarker);
            }

            var existingActors = new List<Transform>();
            if (scope?.Snapshot?.Deck?.ItemSlotUids != null)
            {
                IReadOnlyList<int> itemUids = scope.Snapshot.Deck.ItemSlotUids;
                for (var i = 0; i < itemUids.Count; i++)
                {
                    int uid = itemUids[i];
                    if (uid <= 0 || uid == payload.CardUid)
                    {
                        continue;
                    }

                    Transform actor = registry.ResolveActor(uid);
                    if (actor != null)
                    {
                        existingActors.Add(actor);
                    }
                }
            }

            int totalCount = existingActors.Count + 1;
            var targets = new List<HandCardLayoutTarget>(totalCount);
            mLayoutSolver.BuildLayout(totalCount, targets);
            if (targets.Count == 0)
            {
                BattleTraceHooks.RecordFlowResolve(Id, "layout_empty", "HandCardLayoutSolver returned no targets", payload);
                return new FlowHandle(mFlow, onMarker);
            }

            var existingTargets = new List<HandCardLayoutTarget>(existingActors.Count);
            for (var i = 0; i < existingActors.Count; i++)
            {
                existingTargets.Add(targets[i]);
            }

            HandCardLayoutTarget acquiredTarget = targets[targets.Count - 1];
            mFlow.Play(
                acquired,
                acquiredTarget.LocalPosition,
                acquiredTarget.SortingOrder,
                existingActors,
                existingTargets,
                mHandActorsRoot);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
