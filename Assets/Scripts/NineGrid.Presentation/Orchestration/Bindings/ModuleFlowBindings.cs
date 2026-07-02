using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Flow.Core;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class CardKillFlowBinding : IFlowBinding
    {
        private readonly CardKillFlow mFlow;

        public CardKillFlowBinding(CardKillFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.CardKill;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            int attackerUid = payload.ActorUid > 0 ? payload.ActorUid : PresentationFallbackActorUids.Player;
            int targetUid = payload.CardUid > 0 ? payload.CardUid : payload.TargetUid;
            var player = registry?.ResolveActor(attackerUid);
            var enemy = registry?.ResolveActor(targetUid);
            if (player == null || enemy == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            var direction = AttackDirectionResolver.Resolve(payload, registry);
            mFlow.Play(player, enemy, direction);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    public sealed class BoardRotateFlowBinding : IFlowBinding
    {
        private readonly BoardRotateFlow mFlow;

        public BoardRotateFlowBinding(BoardRotateFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.BoardRotate;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            if (TryPlayOuterRingStep(registry, payload, out List<Transform> actors, out List<Transform> targets))
            {
                mFlow.Play(actors, targets);
                return new FlowHandle(mFlow, onMarker);
            }

            return FlowBindingFallback.SnapshotHandle(onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }

        private static bool TryPlayOuterRingStep(
            IViewRegistry registry,
            FlowPayload payload,
            out List<Transform> actors,
            out List<Transform> targets)
        {
            actors = new List<Transform>(8);
            targets = new List<Transform>(8);
            if (registry == null)
            {
                return false;
            }

            FlowPlaybackScope scope = FlowPlaybackScope.Current;
            if (scope?.Snapshot != null)
            {
                var snapshotRingAnchors = new List<Transform>(8);
                if (DeckFlowActorResolver.TryResolveBoardRingActors(registry, scope.Snapshot, actors, snapshotRingAnchors))
                {
                    BoardRingPath.BuildClockwiseStepTargets(snapshotRingAnchors, payload.Amount, targets);
                    return actors.Count >= 2 && targets.Count == actors.Count;
                }
            }

            IReadOnlyList<SlotId> path = BoardRingPath.ClockwiseOuterRing;
            for (var i = 0; i < path.Count; i++)
            {
                SlotId slot = path[i];
                Transform actor = registry.ResolveActor(PresentationFallbackActorUids.BoardCard(slot.Index));
                Transform anchor = registry.ResolveAnchor(slot);
                if (actor == null || anchor == null)
                {
                    actors.Clear();
                    targets.Clear();
                    return false;
                }

                actors.Add(actor);
            }

            var ringAnchors = new List<Transform>(path.Count);
            for (var i = 0; i < path.Count; i++)
            {
                ringAnchors.Add(registry.ResolveAnchor(path[i]));
            }

            BoardRingPath.BuildClockwiseStepTargets(ringAnchors, payload.Amount, targets);
            return actors.Count >= 2 && targets.Count == actors.Count;
        }
    }

    public sealed class MoveCardFlowBinding : IFlowBinding
    {
        private readonly CardDeckSubstituteFlow mFlow;

        public MoveCardFlowBinding(CardDeckSubstituteFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.MoveCard;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            if (DeckFlowActorResolver.TryResolveCardAndSlot(registry, payload, out Transform card, out Transform slot))
            {
                mFlow.Play(card, slot);
                return new FlowHandle(mFlow, onMarker);
            }

            Transform fallbackCard = ResolveCard(registry, payload);
            Transform fallbackSlot = registry != null && payload.ToSlot.IsBoardSlot
                ? registry.ResolveAnchor(payload.ToSlot)
                : null;
            if (fallbackCard != null && fallbackSlot != null)
            {
                mFlow.Play(fallbackCard, fallbackSlot);
                return new FlowHandle(mFlow, onMarker);
            }

            return FlowBindingFallback.SnapshotHandle(onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }

        private static Transform ResolveCard(IViewRegistry registry, FlowPayload payload)
        {
            if (registry == null)
            {
                return null;
            }

            if (payload.CardUid > 0)
            {
                Transform card = registry.ResolveActor(payload.CardUid);
                if (card != null)
                {
                    return card;
                }
            }

            if (payload.FromSlot.IsBoardSlot)
            {
                FlowPlaybackScope scope = FlowPlaybackScope.Current;
                if (scope?.Snapshot != null)
                {
                    IReadOnlyList<BoardSlotView> slots = scope.Snapshot.Board.Slots;
                    for (var i = 0; i < slots.Count; i++)
                    {
                        BoardSlotView slotView = slots[i];
                        if (slotView.Slot == payload.FromSlot)
                        {
                            return registry.ResolveActor(slotView.CardUid);
                        }
                    }
                }

                return registry.ResolveActor(PresentationFallbackActorUids.BoardCard(payload.FromSlot.Index));
            }

            return null;
        }
    }

    public sealed class CardDealFlowBinding : IFlowBinding
    {
        private readonly CardDeckSubstituteFlow mSubstituteFlow;
        private readonly CardDeckDealFlow mDealFlow;

        public CardDealFlowBinding(CardDeckDealFlow dealFlow, CardDeckSubstituteFlow substituteFlow)
        {
            mDealFlow = dealFlow;
            mSubstituteFlow = substituteFlow;
        }

        public FlowId Id => FlowId.CardDeal;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mSubstituteFlow == null && mDealFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            if (DeckFlowActorResolver.TryResolveCardAndSlot(registry, payload, out Transform card, out Transform slot)
                && mSubstituteFlow != null)
            {
                mSubstituteFlow.Play(card, slot);
                return new FlowHandle(mSubstituteFlow, onMarker);
            }

            if (mDealFlow != null
                && DeckFlowActorResolver.TryBuildDeckEntry(registry, payload, out List<Transform> cards, out List<Transform> slots))
            {
                mDealFlow.Play(cards, slots);
                return new FlowHandle(mDealFlow, onMarker);
            }

            IDirectedFlow directed = mDealFlow != null ? mDealFlow : mSubstituteFlow;
            return new FlowHandle(directed, onMarker);
        }

        public void Stop()
        {
            mDealFlow?.StopAndRestore();
            mSubstituteFlow?.StopAndRestore();
        }
    }

    public sealed class FillSlotsFlowBinding : IFlowBinding
    {
        private readonly CardDeckSubstituteFlow mFlow;

        public FillSlotsFlowBinding(CardDeckSubstituteFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.FillSlots;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            var dealPayload = new FlowPayload
            {
                CardUid = payload?.CardUid ?? 0,
                ToSlot = payload?.ToSlot ?? SlotId.None,
                Amount = payload?.Amount ?? 0,
            };

            if (DeckFlowActorResolver.TryResolveCardAndSlot(registry, dealPayload, out Transform card, out Transform slot))
            {
                mFlow.Play(card, slot);
                return new FlowHandle(mFlow, onMarker);
            }

            return FlowBindingFallback.SnapshotHandle(onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    public sealed class UseItemFlowBinding : IFlowBinding
    {
        private readonly ItemUseFlow mFlow;

        public UseItemFlowBinding(ItemUseFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.UseItem;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            int itemUid = payload.CardUid > 0 ? payload.CardUid : payload.ActorUid;
            Transform actor = itemUid > 0 ? registry?.ResolveActor(itemUid) : null;
            if (actor == null)
            {
                return FlowBindingFallback.SnapshotHandle(onMarker);
            }

            mFlow.Play(actor);
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    public sealed class CardDeckEntryFlowBinding : IFlowBinding
    {
        private readonly CardDeckEntryFlow mFlow;

        public CardDeckEntryFlowBinding(CardDeckEntryFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.CardDeckEntry;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            if (mFlow == null)
            {
                return new FlowHandle(null, onMarker);
            }

            if (DeckFlowActorResolver.TryBuildDeckEntry(registry, payload, out List<Transform> cards, out List<Transform> slots))
            {
                mFlow.Play(cards, slots);
                return new FlowHandle(mFlow, onMarker);
            }

            return FlowBindingFallback.SnapshotHandle(onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }

    internal static class FlowBindingFallback
    {
        public static FlowHandle SnapshotHandle(Action<string> onMarker)
        {
            return new FlowHandle(InstantAlignFlow.Instance, onMarker);
        }

        private sealed class InstantAlignFlow : IDirectedFlow
        {
            public static readonly InstantAlignFlow Instance = new InstantAlignFlow();
            public bool IsPlaying => false;
            public float ExpectedDuration => 0f;
            public void StopAndRestore() { }
        }
    }
}
