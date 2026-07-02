using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Orchestration
{
    internal static class DeckFlowActorResolver
    {
        public static bool TryResolveCardAndSlot(
            IViewRegistry registry,
            FlowPayload payload,
            out Transform card,
            out Transform slot)
        {
            card = null;
            slot = null;
            if (registry == null || payload == null)
            {
                return false;
            }

            if (payload.CardUid > 0)
            {
                card = registry.ResolveActor(payload.CardUid);
            }

            if (payload.ToSlot.IsBoardSlot)
            {
                slot = registry.ResolveAnchor(payload.ToSlot);
            }

            if (card != null && slot != null)
            {
                return true;
            }

            FlowPlaybackScope scope = FlowPlaybackScope.Current;
            if (scope?.ActorFactory == null || scope.Snapshot == null)
            {
                return card != null && slot != null;
            }

            if (card == null && payload.CardUid > 0 && scope.Snapshot.TryGetCard(payload.CardUid, out CardView cardView))
            {
                card = scope.ActorFactory.Spawn(cardView.DefId, payload.CardUid);
                PlaceAtDeckOrigin(registry, card);
            }

            return card != null && slot != null;
        }

        public static bool TryBuildDeckEntry(
            IViewRegistry registry,
            FlowPayload payload,
            out List<Transform> cards,
            out List<Transform> slots)
        {
            cards = new List<Transform>();
            slots = new List<Transform>();
            FlowPlaybackScope scope = FlowPlaybackScope.Current;
            if (registry == null || scope?.Snapshot?.Deck == null || scope.ActorFactory == null)
            {
                return false;
            }

            IReadOnlyList<int> drawPile = scope.Snapshot.Deck.DrawPileUids;
            if (drawPile == null || drawPile.Count == 0)
            {
                return false;
            }

            int count = payload != null && payload.Amount > 0
                ? Mathf.Min(payload.Amount, drawPile.Count)
                : drawPile.Count;

            ResolveDeckSlotAnchors(registry, slots, count);
            if (slots.Count == 0)
            {
                return false;
            }

            count = Mathf.Min(count, slots.Count);
            Transform origin = ResolveDeckEntryOrigin(registry);
            for (var i = 0; i < count; i++)
            {
                int uid = drawPile[i];
                if (uid <= 0)
                {
                    continue;
                }

                string defId = scope.Snapshot.TryGetCard(uid, out CardView cardView)
                    ? cardView.DefId
                    : string.Empty;
                Transform actor = scope.ActorFactory.TryGet(uid, out Transform existing) && existing != null
                    ? existing
                    : scope.ActorFactory.Spawn(defId, uid);
                if (actor == null)
                {
                    continue;
                }

                if (origin != null)
                {
                    actor.position = origin.position;
                }

                registry.RegisterActor(uid, actor);
                cards.Add(actor);
            }

            while (slots.Count > cards.Count)
            {
                slots.RemoveAt(slots.Count - 1);
            }

            return cards.Count > 0;
        }

        public static bool TryResolveBoardRingActors(
            IViewRegistry registry,
            CoreViewSnapshot snapshot,
            List<Transform> actors,
            List<Transform> slotAnchors)
        {
            actors?.Clear();
            slotAnchors?.Clear();
            if (registry == null || snapshot?.Board == null || actors == null || slotAnchors == null)
            {
                return false;
            }

            IReadOnlyList<SlotId> path = BoardRingPath.ClockwiseOuterRing;
            for (var i = 0; i < path.Count; i++)
            {
                SlotId slot = path[i];
                Transform anchor = registry.ResolveAnchor(slot);
                if (anchor == null)
                {
                    actors.Clear();
                    slotAnchors.Clear();
                    return false;
                }

                slotAnchors.Add(anchor);
                int uid = FindCardUidAtSlot(snapshot, slot);
                Transform actor = uid > 0 ? registry.ResolveActor(uid) : null;
                if (actor == null && uid > 0)
                {
                    FlowPlaybackScope scope = FlowPlaybackScope.Current;
                    if (scope?.ActorFactory != null && snapshot.TryGetCard(uid, out CardView cardView))
                    {
                        actor = scope.ActorFactory.Spawn(cardView.DefId, uid);
                        registry.RegisterActor(uid, actor);
                    }
                }

                if (actor == null)
                {
                    actors.Clear();
                    slotAnchors.Clear();
                    return false;
                }

                actors.Add(actor);
            }

            return actors.Count >= 2;
        }

        private static int FindCardUidAtSlot(CoreViewSnapshot snapshot, SlotId slot)
        {
            IReadOnlyList<BoardSlotView> slots = snapshot.Board.Slots;
            for (var i = 0; i < slots.Count; i++)
            {
                BoardSlotView slotView = slots[i];
                if (slotView.Slot == slot)
                {
                    return slotView.CardUid;
                }
            }

            return 0;
        }

        private static Transform ResolveNamedAnchor(IViewRegistry registry, string id)
        {
            if (registry is TableNineViewRegistry production)
            {
                return production.ResolveNamedAnchor(id);
            }

            return null;
        }

        private static void ResolveDeckSlotAnchors(IViewRegistry registry, List<Transform> slots, int count)
        {
            for (var i = 1; i <= count; i++)
            {
                Transform anchor = ResolveNamedAnchor(registry, $"deck.slot{i}");
                if (anchor != null)
                {
                    slots.Add(anchor);
                }
            }
        }

        private static Transform ResolveDeckEntryOrigin(IViewRegistry registry)
        {
            return ResolveNamedAnchor(registry, $"deck.{SceneStagingAnchorUtil.DeckEntryPreparationSlotName}")
                ?? ResolveNamedAnchor(registry, "deck");
        }

        private static void PlaceAtDeckOrigin(IViewRegistry registry, Transform card)
        {
            Transform origin = ResolveDeckOrigin(registry);
            if (origin != null && card != null)
            {
                card.position = origin.position;
            }
        }

        private static Transform ResolveDeckOrigin(IViewRegistry registry)
        {
            return ResolveNamedAnchor(registry, $"deck.{SceneStagingAnchorUtil.DeckDealOriginSlotName}")
                ?? ResolveNamedAnchor(registry, "deck");
        }
    }
}
