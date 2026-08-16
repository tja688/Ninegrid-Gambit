using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 房内货架/候选落格：优先偏好格，避开 Avatar 站位，并保证 PreferEmpty 空路到离开格（#92/#93）。
    /// </summary>
    public static class InRoomOfferSlotPlanner
    {
        public const int DefaultLeaveSlot = 8;
        public const int DefaultRefreshSlot = 2;
        public const int DefaultAvatarSlot = 5;

        private static readonly List<SlotId> sPathScratch = new List<SlotId>(8);

        /// <summary>
        /// 为 <paramref name="count"/> 个条目分配落格；每项优先 <paramref name="preferredPerIndex"/>[i]，
        /// 冲突时从 <paramref name="fallbackPool"/> 选取，并校验 Avatar→Leave 的 PreferEmpty 通路。
        /// <paramref name="excludedSlots"/> 为绝对禁区（如悬停预览不可覆盖的图标/站位格）。
        /// </summary>
        public static int[] Plan(
            BoardModel board,
            int count,
            int avatarSlot,
            IReadOnlyList<int> preferredPerIndex,
            IReadOnlyList<int> fallbackPool,
            int leaveSlot = DefaultLeaveSlot,
            int refreshSlot = DefaultRefreshSlot,
            bool reserveRefresh = true,
            IReadOnlyCollection<int> excludedSlots = null)
        {
            if (count <= 0)
            {
                return new int[0];
            }

            var result = new int[count];
            var used = new HashSet<int> { leaveSlot };
            if (reserveRefresh)
            {
                used.Add(refreshSlot);
            }

            for (var i = 0; i < count; i++)
            {
                var preferred = preferredPerIndex != null && i < preferredPerIndex.Count
                    ? preferredPerIndex[i]
                    : 0;
                var slot = PickSlot(
                    board,
                    avatarSlot,
                    leaveSlot,
                    refreshSlot,
                    used,
                    preferred,
                    fallbackPool,
                    excludedSlots);
                result[i] = slot;
                if (slot > 0)
                {
                    used.Add(slot);
                }
            }

            return result;
        }

        /// <summary>单条目落格：偏好格被占或与 Avatar 重叠时从 fallback 重选。</summary>
        public static int PlanOne(
            BoardModel board,
            int avatarSlot,
            int preferredSlot,
            IReadOnlyList<int> fallbackPool,
            IReadOnlyCollection<int> alreadyUsed,
            int leaveSlot = DefaultLeaveSlot,
            int refreshSlot = DefaultRefreshSlot,
            bool reserveRefresh = true,
            IReadOnlyCollection<int> excludedSlots = null)
        {
            var used = new HashSet<int>(alreadyUsed ?? new int[0]);
            used.Add(leaveSlot);
            if (reserveRefresh)
            {
                used.Add(refreshSlot);
            }

            return PickSlot(
                board,
                avatarSlot,
                leaveSlot,
                refreshSlot,
                used,
                preferredSlot,
                fallbackPool,
                excludedSlots);
        }

        private static int PickSlot(
            BoardModel board,
            int avatarSlot,
            int leaveSlot,
            int refreshSlot,
            HashSet<int> used,
            int preferred,
            IReadOnlyList<int> fallbackPool,
            IReadOnlyCollection<int> excludedSlots = null)
        {
            var candidates = new List<int>(8);
            TryAddCandidate(candidates, preferred, avatarSlot, leaveSlot, refreshSlot, used, excludedSlots);

            if (fallbackPool != null)
            {
                for (var i = 0; i < fallbackPool.Count; i++)
                {
                    TryAddCandidate(candidates, fallbackPool[i], avatarSlot, leaveSlot, refreshSlot, used, excludedSlots);
                }
            }

            for (var s = SlotId.MinBoardIndex; s <= SlotId.MaxBoardIndex; s++)
            {
                TryAddCandidate(candidates, s, avatarSlot, leaveSlot, refreshSlot, used, excludedSlots);
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var slot = candidates[i];
                if (IsPlacementValid(board, avatarSlot, leaveSlot, used, slot))
                {
                    return slot;
                }
            }

            return preferred > 0 && preferred != avatarSlot && preferred != leaveSlot
                ? preferred
                : (candidates.Count > 0 ? candidates[0] : 0);
        }

        private static void TryAddCandidate(
            List<int> candidates,
            int slot,
            int avatarSlot,
            int leaveSlot,
            int refreshSlot,
            HashSet<int> used,
            IReadOnlyCollection<int> excludedSlots)
        {
            if (slot < SlotId.MinBoardIndex || slot > SlotId.MaxBoardIndex)
            {
                return;
            }

            if (slot == avatarSlot || slot == leaveSlot || slot == refreshSlot || used.Contains(slot))
            {
                return;
            }

            if (IsExcluded(excludedSlots, slot))
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == slot)
                {
                    return;
                }
            }

            candidates.Add(slot);
        }

        private static bool IsExcluded(IReadOnlyCollection<int> excludedSlots, int slot)
        {
            if (excludedSlots == null)
            {
                return false;
            }

            foreach (var excluded in excludedSlots)
            {
                if (excluded == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPlacementValid(
            BoardModel board,
            int avatarSlot,
            int leaveSlot,
            HashSet<int> used,
            int candidateSlot)
        {
            if (board == null || candidateSlot <= 0)
            {
                return false;
            }

            var trialSoft = new HashSet<int>(used) { candidateSlot };
            return HasPreferEmptyPathToLeave(board, avatarSlot, leaveSlot, trialSoft);
        }

        private static bool HasPreferEmptyPathToLeave(
            BoardModel board,
            int avatarSlot,
            int leaveSlot,
            HashSet<int> softBlockedSlots)
        {
            if (board == null || avatarSlot <= 0 || leaveSlot <= 0)
            {
                return true;
            }

            var from = SlotId.Board(avatarSlot);
            var to = SlotId.Board(leaveSlot);
            return AvatarWalkPathfinder.TryFindPath(
                board,
                from,
                to,
                sPathScratch,
                slot => slot.Index == leaveSlot,
                slot => softBlockedSlots.Contains(slot.Index));
        }
    }
}
