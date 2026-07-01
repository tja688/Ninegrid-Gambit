using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
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

            int attackerUid = payload.ActorUid > 0 ? payload.ActorUid : PerformanceDebugActorUids.Player;
            int targetUid = payload.CardUid > 0 ? payload.CardUid : payload.TargetUid;
            var player = registry?.ResolveActor(attackerUid);
            var enemy = registry?.ResolveActor(targetUid);
            if (player == null || enemy == null)
            {
                return new FlowHandle(mFlow, onMarker);
            }

            var direction = payload.Direction.sqrMagnitude > 0.0001f ? payload.Direction : Vector2.right;
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

            var actors = CollectBoardActors(registry);
            var slots = CollectBoardSlotAnchors(registry);
            if (actors.Count >= 2 && slots.Count >= 2)
            {
                mFlow.PlayRingStep(actors, slots, payload.Amount >= 0 ? 1 : -1);
                return new FlowHandle(mFlow, onMarker);
            }

            mFlow.PlayPreview();
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }

        private static List<Transform> CollectBoardActors(IViewRegistry registry)
        {
            var actors = new List<Transform>(9);
            if (registry == null)
            {
                return actors;
            }

            for (var i = 1; i <= 9; i++)
            {
                Transform actor = registry.ResolveActor(PerformanceDebugActorUids.BoardCard(i));
                if (actor != null)
                {
                    actors.Add(actor);
                }
            }

            return actors;
        }

        private static List<Transform> CollectBoardSlotAnchors(IViewRegistry registry)
        {
            var slots = new List<Transform>(9);
            if (registry == null)
            {
                return slots;
            }

            for (var i = 1; i <= 9; i++)
            {
                Transform slot = registry.ResolveAnchor(SlotId.Board(i));
                if (slot != null)
                {
                    slots.Add(slot);
                }
            }

            return slots;
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

            Transform card = ResolveCard(registry, payload);
            Transform slot = registry != null ? registry.ResolveAnchor(payload.ToSlot) : null;
            if (card == null || slot == null)
            {
                mFlow.PlayPreview();
                return new FlowHandle(mFlow, onMarker);
            }

            mFlow.Play(card, slot);
            return new FlowHandle(mFlow, onMarker);
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
                return registry.ResolveActor(PerformanceDebugActorUids.BoardCard(payload.FromSlot.Index));
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

            Transform card = payload.CardUid > 0 ? registry?.ResolveActor(payload.CardUid) : null;
            Transform slot = registry != null ? registry.ResolveAnchor(payload.ToSlot) : null;
            if (card != null && slot != null && mSubstituteFlow != null)
            {
                mSubstituteFlow.Play(card, slot);
                return new FlowHandle(mSubstituteFlow, onMarker);
            }

            mDealFlow?.PlayPreview();
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

            mFlow.PlayPreview();
            return new FlowHandle(mFlow, onMarker);
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
            mFlow.Play(itemUid);
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

            mFlow.PlayPreview();
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
