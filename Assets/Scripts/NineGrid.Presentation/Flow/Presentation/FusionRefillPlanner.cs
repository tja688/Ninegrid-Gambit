using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 融合伴随补牌：牌堆重排与排除结果 uid（Flow 策略，Core 只跑 FillEmptySlots）。
    /// </summary>
    public static class FusionRefillPlanner
    {
        public static bool TryCollectResultUids(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex,
            List<int> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException("into");
            }

            into.Clear();
            var fusions = SkeletonFusionPresentationScanner.Collect(entries, startIndex);
            if (fusions.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < fusions.Count; i++)
            {
                var uid = fusions[i].ResultUid;
                if (uid > 0 && !into.Contains(uid))
                {
                    into.Add(uid);
                }
            }

            return into.Count > 0;
        }

        public static bool HasRefillCandidateExcluding(DeckModel deck, IReadOnlyList<int> excludeUids)
        {
            if (deck == null)
            {
                return false;
            }

            return HasRefillCandidateExcludingOrder(deck.DrawPileUids, excludeUids);
        }

        public static bool HasRefillCandidateExcludingOrder(
            IReadOnlyList<int> orderedUids,
            IReadOnlyList<int> excludeUids)
        {
            if (orderedUids == null || orderedUids.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < orderedUids.Count; i++)
            {
                if (!IsExcluded(orderedUids[i], excludeUids))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasEmptyBoardSlot(BoardModel board)
        {
            if (board == null)
            {
                return false;
            }

            var avatarSlot = board.AvatarSlot.Value;
            for (var s = SlotId.MinBoardIndex; s <= SlotId.MaxBoardIndex; s++)
            {
                var slot = SlotId.Board(s);
                if (slot == avatarSlot)
                {
                    continue;
                }

                if (board.GetCardUid(slot) <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static List<int> BuildRefillDrawOrder(IReadOnlyList<int> original, IReadOnlyList<int> excludeUids)
        {
            var nonFusion = new List<int>(original != null ? original.Count : 0);
            var fusionTail = new List<int>(excludeUids != null ? excludeUids.Count : 0);
            if (original == null)
            {
                return nonFusion;
            }

            for (var i = 0; i < original.Count; i++)
            {
                if (IsExcluded(original[i], excludeUids))
                {
                    fusionTail.Add(original[i]);
                }
                else
                {
                    nonFusion.Add(original[i]);
                }
            }

            nonFusion.AddRange(fusionTail);
            return nonFusion;
        }

        /// <summary>
        /// Fill 后按原序恢复仍在牌堆中的 uid，避免把已入场牌写回牌堆。
        /// </summary>
        public static List<int> RestoreRemainingOrder(
            IReadOnlyList<int> originalOrder,
            IReadOnlyList<int> remainingInPile)
        {
            var restored = new List<int>(remainingInPile != null ? remainingInPile.Count : 0);
            if (originalOrder == null || remainingInPile == null || remainingInPile.Count == 0)
            {
                return restored;
            }

            var still = new HashSet<int>(remainingInPile);
            for (var i = 0; i < originalOrder.Count; i++)
            {
                var uid = originalOrder[i];
                if (still.Remove(uid))
                {
                    restored.Add(uid);
                }
            }

            foreach (var uid in still)
            {
                restored.Add(uid);
            }

            return restored;
        }

        public static PostKillCardDeal[] FilterDealsExcluding(
            PostKillCardDeal[] deals,
            IReadOnlyList<int> excludeUids)
        {
            if (deals == null || deals.Length == 0)
            {
                return Array.Empty<PostKillCardDeal>();
            }

            if (excludeUids == null || excludeUids.Count == 0)
            {
                return deals;
            }

            var filtered = new List<PostKillCardDeal>(deals.Length);
            for (var i = 0; i < deals.Length; i++)
            {
                if (!IsExcluded(deals[i].Uid, excludeUids))
                {
                    filtered.Add(deals[i]);
                }
            }

            return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<PostKillCardDeal>();
        }

        private static bool IsExcluded(int uid, IReadOnlyList<int> excludeUids)
        {
            if (excludeUids == null || uid <= 0)
            {
                return false;
            }

            for (var i = 0; i < excludeUids.Count; i++)
            {
                if (excludeUids[i] == uid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
