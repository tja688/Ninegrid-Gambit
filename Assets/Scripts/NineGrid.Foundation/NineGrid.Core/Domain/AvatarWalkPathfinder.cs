using System.Collections.Generic;

namespace NineGrid.Core
{
    /// <summary>
    /// 九宫正交 BFS：途经与终点须为空格（v1；后续可选图标终点再放宽）。
    /// </summary>
    public static class AvatarWalkPathfinder
    {
        /// <summary>
        /// 求从 <paramref name="from"/> 到 <paramref name="to"/> 的最短正交路径（不含起点，含终点）。
        /// 同格时 path 为空并返回 true。
        /// </summary>
        public static bool TryFindPath(
            BoardModel board,
            SlotId from,
            SlotId to,
            List<SlotId> pathOut)
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

            if (!IsWalkable(board, to, from))
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

                    if (!IsWalkable(board, slot, from))
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

        /// <summary>下一步邻格（路径首步）；无路径返回 false。</summary>
        public static bool TryGetNextStep(
            BoardModel board,
            SlotId from,
            SlotId to,
            out SlotId next)
        {
            next = SlotId.None;
            var path = new List<SlotId>(4);
            if (!TryFindPath(board, from, to, path) || path.Count == 0)
            {
                return false;
            }

            next = path[0];
            return true;
        }

        private static bool IsWalkable(BoardModel board, SlotId slot, SlotId avatarSlot)
        {
            if (slot == avatarSlot)
            {
                return true;
            }

            return board.IsEmpty(slot);
        }
    }
}
