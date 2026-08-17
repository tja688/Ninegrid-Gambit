using System;
using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IBoardStabilizationSystem : ISystem
    {
        bool NeedsRefill { get; }
        bool IsRefillSuspended { get; set; }
        SlotId PriorityRefillSlot { get; set; }
        int PendingEmptySlotCount { get; }
        CoreCommandResult ResolveNextSlice();
        int ResolveUntilStable(int maxSlices = 64);
        void DeferDrawUid(int uid, bool restoreToTop);
        void Complete();
    }

    /// <summary>
    /// Core authority for the stable-boundary invariant: an interactive board may not
    /// expose a fillable vacancy while the draw pile can still supply a card.
    /// Each slice resolves one Fill action so Presentation can ack between slices.
    /// </summary>
    public sealed class BoardStabilizationSystem : AbstractSystem, IBoardStabilizationSystem
    {
        private readonly HashSet<int> mDeferredDrawUids = new HashSet<int>();
        private readonly List<int> mDeferredTopUids = new List<int>();

        public bool IsRefillSuspended { get; set; }
        public SlotId PriorityRefillSlot { get; set; }

        public bool NeedsRefill
        {
            get
            {
                if (IsRefillSuspended)
                {
                    if (PriorityRefillSlot.IsBoardSlot)
                    {
                        var boardModel = this.GetModel<BoardModel>();
                        var deckModel = this.GetModel<DeckModel>();
                        return boardModel != null
                            && deckModel != null
                            && deckModel.DrawPileUids.Count > 0
                            && PriorityRefillSlot != boardModel.AvatarSlot.Value
                            && boardModel.IsEmpty(PriorityRefillSlot);
                    }

                    return false;
                }

                var phase = this.GetSystem<IPhaseSystem>();
                if (phase == null || phase.CurrentPhase != GamePhase.InteractionLoop)
                {
                    return false;
                }

                var deckSystem = this.GetSystem<IDeckSystem>();
                if (deckSystem != null && deckSystem.IsNodeCleared())
                {
                    return false;
                }

                var deck = this.GetModel<DeckModel>();
                return deck != null
                    && deck.DrawPileUids.Count > 0
                    && PendingEmptySlotCount > 0;
            }
        }

        public int PendingEmptySlotCount
        {
            get
            {
                var board = this.GetModel<BoardModel>();
                if (board == null)
                {
                    return 0;
                }

                var empty = 0;
                var order = FillEmptySlotsAction.FillOrder;
                for (var i = 0; i < order.Count; i++)
                {
                    var slot = order[i];
                    if (slot != board.AvatarSlot.Value && board.IsEmpty(slot))
                    {
                        empty++;
                    }
                }

                return empty;
            }
        }

        protected override void OnInit()
        {
            IsRefillSuspended = false;
            PriorityRefillSlot = SlotId.None;
            mDeferredDrawUids.Clear();
            mDeferredTopUids.Clear();
        }

        public CoreCommandResult ResolveNextSlice()
        {
            if (!NeedsRefill)
            {
                Complete();
                return CoreCommandResult.Accept(0);
            }

            var prioritySlot = PriorityRefillSlot;
            PriorityRefillSlot = SlotId.None;

            var deck = this.GetModel<DeckModel>();
            PruneDeferredUids(deck.DrawPileUids);
            var originalOrder = new List<int>(deck.DrawPileUids);
            var refillOrder = BuildRefillOrder(originalOrder);
            if (refillOrder.Count == 0)
            {
                Complete();
                return CoreCommandResult.Accept(0);
            }

            deck.ReorderDrawPile(refillOrder);
            int resolved;
            try
            {
                resolved = this.GetSystem<IActionPipelineSystem>().Execute(new FillEmptySlotsAction(prioritySlot));
            }
            finally
            {
                deck.ReorderDrawPile(RestoreRemainingOrder(originalOrder, deck.DrawPileUids));
            }

            PruneDeferredUids(deck.DrawPileUids);
            if (!NeedsRefill)
            {
                Complete();
            }

            return CoreCommandResult.Accept(resolved);
        }

        public int ResolveUntilStable(int maxSlices = 64)
        {
            var resolved = 0;
            var slices = 0;
            while (NeedsRefill)
            {
                if (slices++ >= Math.Max(1, maxSlices))
                {
                    throw new InvalidOperationException("Board stabilization exceeded the refill slice budget.");
                }

                var result = ResolveNextSlice();
                resolved += result.ResolvedActions;
            }

            Complete();
            return resolved;
        }

        public void DeferDrawUid(int uid, bool restoreToTop)
        {
            if (uid <= 0 || !mDeferredDrawUids.Add(uid))
            {
                return;
            }

            if (restoreToTop)
            {
                mDeferredTopUids.Insert(0, uid);
            }
        }

        public void Complete()
        {
            PriorityRefillSlot = SlotId.None;
            RestoreDeferredTopOrder();
            mDeferredDrawUids.Clear();
            mDeferredTopUids.Clear();
        }

        private List<int> BuildRefillOrder(IReadOnlyList<int> originalOrder)
        {
            if (mDeferredDrawUids.Count == 0)
            {
                return new List<int>(originalOrder);
            }

            var immediate = new List<int>(originalOrder.Count);
            var deferred = new List<int>(mDeferredDrawUids.Count);
            for (var i = 0; i < originalOrder.Count; i++)
            {
                var uid = originalOrder[i];
                if (mDeferredDrawUids.Contains(uid))
                {
                    deferred.Add(uid);
                }
                else
                {
                    immediate.Add(uid);
                }
            }

            // If only deferred fusion results remain, allow them rather than exposing a permanent hole.
            if (immediate.Count == 0)
            {
                return new List<int>(originalOrder);
            }

            immediate.AddRange(deferred);
            return immediate;
        }

        private void PruneDeferredUids(IReadOnlyList<int> drawPileUids)
        {
            if (mDeferredDrawUids.Count == 0)
            {
                return;
            }

            var remaining = new HashSet<int>(drawPileUids);
            mDeferredDrawUids.RemoveWhere(uid => !remaining.Contains(uid));
            mDeferredTopUids.RemoveAll(uid => !remaining.Contains(uid));
        }

        private void RestoreDeferredTopOrder()
        {
            if (mDeferredTopUids.Count == 0)
            {
                return;
            }

            var deck = this.GetModel<DeckModel>();
            var current = new List<int>(deck.DrawPileUids);
            var restored = new List<int>(current.Count);
            for (var i = 0; i < mDeferredTopUids.Count; i++)
            {
                var uid = mDeferredTopUids[i];
                if (current.Remove(uid))
                {
                    restored.Add(uid);
                }
            }

            restored.AddRange(current);
            deck.ReorderDrawPile(restored);
        }

        private static List<int> RestoreRemainingOrder(
            IReadOnlyList<int> originalOrder,
            IReadOnlyList<int> remainingInPile)
        {
            var restored = new List<int>(remainingInPile.Count);
            var remaining = new HashSet<int>(remainingInPile);
            for (var i = 0; i < originalOrder.Count; i++)
            {
                var uid = originalOrder[i];
                if (remaining.Remove(uid))
                {
                    restored.Add(uid);
                }
            }

            foreach (var uid in remaining)
            {
                restored.Add(uid);
            }

            return restored;
        }
    }
}
