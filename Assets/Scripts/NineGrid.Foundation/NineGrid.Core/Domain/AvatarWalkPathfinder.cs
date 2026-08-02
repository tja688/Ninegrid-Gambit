using System;
using System.Collections.Generic;

namespace NineGrid.Core
{
    /// <summary>
    /// 九宫正交 BFS（ADR-0019 / ADR-0020）：
    /// 终点可为空格或目标图标格；途经优先完全空置（绕开软占/真卡），无空路时回退允许踩软占。
    /// 无谓词的重载保持旧行为：途经与终点皆须空格，无软占回退。
    /// </summary>
    public static class AvatarWalkPathfinder
    {
        /// <summary>
        /// 求从 <paramref name="from"/> 到 <paramref name="to"/> 的最短正交路径（不含起点，含终点）。
        /// 同格时 path 为空并返回 true。途经与终点须为空格（兼容重载，无软占回退）。
        /// </summary>
        public static bool TryFindPath(
            BoardModel board,
            SlotId from,
            SlotId to,
            List<SlotId> pathOut)
        {
            return TryFindPathCore(
                board,
                from,
                to,
                pathOut,
                isDestination: null,
                isSoftBlocked: null,
                allowSoftFallback: false);
        }

        /// <summary>
        /// 两阶段寻路：先 PreferEmpty（途经空且非软占），失败再 AllowSoft（途经可踩软占/真卡）。
        /// <paramref name="isDestination"/> 为真的格可作为终点（即使非空或软占）。
        /// </summary>
        public static bool TryFindPath(
            BoardModel board,
            SlotId from,
            SlotId to,
            List<SlotId> pathOut,
            Func<SlotId, bool> isDestination,
            Func<SlotId, bool> isSoftBlocked)
        {
            return TryFindPathCore(
                board,
                from,
                to,
                pathOut,
                isDestination,
                isSoftBlocked,
                allowSoftFallback: true);
        }

        /// <summary>下一步邻格（路径首步）；无路径返回 false。</summary>
        public static bool TryGetNextStep(
            BoardModel board,
            SlotId from,
            SlotId to,
            out SlotId next)
        {
            return TryGetNextStep(board, from, to, out next, null, null);
        }

        /// <summary>下一步邻格；带终点/软占谓词时走两阶段寻路。</summary>
        public static bool TryGetNextStep(
            BoardModel board,
            SlotId from,
            SlotId to,
            out SlotId next,
            Func<SlotId, bool> isDestination,
            Func<SlotId, bool> isSoftBlocked)
        {
            next = SlotId.None;
            var path = new List<SlotId>(4);
            var ok = isDestination == null && isSoftBlocked == null
                ? TryFindPath(board, from, to, path)
                : TryFindPath(board, from, to, path, isDestination, isSoftBlocked);
            if (!ok || path.Count == 0)
            {
                return false;
            }

            next = path[0];
            return true;
        }

        private static bool TryFindPathCore(
            BoardModel board,
            SlotId from,
            SlotId to,
            List<SlotId> pathOut,
            Func<SlotId, bool> isDestination,
            Func<SlotId, bool> isSoftBlocked,
            bool allowSoftFallback)
        {
            if (pathOut == null)
            {
                return false;
            }

            pathOut.Clear();
            if (board == null || !from.IsBoardSlot || !to.IsBoardSlot)
            {
                return false;
            }

            if (from == to)
            {
                return true;
            }

            if (TryBfs(board, from, to, pathOut, isDestination, isSoftBlocked, preferEmpty: true))
            {
                return true;
            }

            if (!allowSoftFallback)
            {
                return false;
            }

            pathOut.Clear();
            return TryBfs(board, from, to, pathOut, isDestination, isSoftBlocked, preferEmpty: false);
        }

        private static bool TryBfs(
            BoardModel board,
            SlotId from,
            SlotId to,
            List<SlotId> pathOut,
            Func<SlotId, bool> isDestination,
            Func<SlotId, bool> isSoftBlocked,
            bool preferEmpty)
        {
            if (!IsWalkable(board, to, from, to, isDestination, isSoftBlocked, preferEmpty))
            {
                return false;
            }

            var cameFrom = new int[SlotId.MaxBoardIndex + 1];
            var visited = new bool[SlotId.MaxBoardIndex + 1];
            for (var i = 0; i < cameFrom.Length; i++)
            {
                cameFrom[i] = -1;
            }

            var queue = new Queue<int>(9);
            queue.Enqueue(from.Index);
            visited[from.Index] = true;

            var found = false;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == to.Index)
                {
                    found = true;
                    break;
                }

                var currentSlot = SlotId.Board(current);
                for (var candidate = SlotId.MinBoardIndex; candidate <= SlotId.MaxBoardIndex; candidate++)
                {
                    if (visited[candidate])
                    {
                        continue;
                    }

                    var slot = SlotId.Board(candidate);
                    if (!currentSlot.IsAdjacentTo(slot))
                    {
                        continue;
                    }

                    if (!IsWalkable(board, slot, from, to, isDestination, isSoftBlocked, preferEmpty))
                    {
                        continue;
                    }

                    visited[candidate] = true;
                    cameFrom[candidate] = current;
                    queue.Enqueue(candidate);
                }
            }

            if (!found)
            {
                return false;
            }

            var chain = new List<int>(4);
            for (var cursor = to.Index; cursor != from.Index;)
            {
                chain.Add(cursor);
                var prev = cameFrom[cursor];
                if (prev < 0)
                {
                    pathOut.Clear();
                    return false;
                }

                cursor = prev;
            }

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                pathOut.Add(SlotId.Board(chain[i]));
            }

            return pathOut.Count > 0;
        }

        private static bool IsWalkable(
            BoardModel board,
            SlotId slot,
            SlotId avatarSlot,
            SlotId destination,
            Func<SlotId, bool> isDestination,
            Func<SlotId, bool> isSoftBlocked,
            bool preferEmpty)
        {
            if (slot == avatarSlot)
            {
                return true;
            }

            var isDest = slot == destination;
            if (isDest)
            {
                if (board.IsEmpty(slot))
                {
                    return true;
                }

                return isDestination != null && isDestination(slot);
            }

            // 途经
            if (preferEmpty)
            {
                if (!board.IsEmpty(slot))
                {
                    return false;
                }

                if (isSoftBlocked != null && isSoftBlocked(slot))
                {
                    return false;
                }

                return true;
            }

            // AllowSoft：途经可踩真卡与软占
            return true;
        }
    }
}
