using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Presentation.Shared
{
    /// <summary>
    /// 九宫格对战方向：以「向右攻击」为烘焙基准，运行时旋转向量推广。
    /// </summary>
    public enum CardBattleDirection
    {
        Right = 0,
        Up = 1,
        Left = 2,
        Down = 3,
    }

    public static class CardBattleDirectionUtil
    {
        private static readonly Vector2[] Cycle =
        {
            Vector2.right,
            Vector2.up,
            Vector2.left,
            Vector2.down,
        };

        public static Vector2 ToVector2(CardBattleDirection direction)
        {
            int index = (int)direction;
            return index >= 0 && index < Cycle.Length ? Cycle[index] : Vector2.right;
        }

        /// <summary>从棋盘正交相邻槽位推导攻击方向；斜向相邻返回 false。</summary>
        public static bool TryFromBoardSlots(SlotId from, SlotId to, out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!from.IsBoardSlot || !to.IsBoardSlot)
            {
                return false;
            }

            int deltaRow = to.Row - from.Row;
            int deltaColumn = to.Column - from.Column;
            if (deltaRow == 0 && deltaColumn == 1)
            {
                direction = Vector2.right;
                return true;
            }

            if (deltaRow == 0 && deltaColumn == -1)
            {
                direction = Vector2.left;
                return true;
            }

            if (deltaRow == -1 && deltaColumn == 0)
            {
                direction = Vector2.up;
                return true;
            }

            if (deltaRow == 1 && deltaColumn == 0)
            {
                direction = Vector2.down;
                return true;
            }

            return false;
        }

        public static CardBattleDirection NextInCycle(ref int cursor)
        {
            CardBattleDirection current = (CardBattleDirection)(cursor % Cycle.Length);
            cursor = (cursor + 1) % Cycle.Length;
            return current;
        }

        public static CardBattleDirection Opposite(CardBattleDirection direction)
        {
            return direction switch
            {
                CardBattleDirection.Right => CardBattleDirection.Left,
                CardBattleDirection.Left => CardBattleDirection.Right,
                CardBattleDirection.Up => CardBattleDirection.Down,
                CardBattleDirection.Down => CardBattleDirection.Up,
                _ => direction,
            };
        }

        public static Vector2 Opposite(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector2.left;
            }

            return -direction.normalized;
        }

        /// <summary>将「向右」烘焙空间偏移旋转到目标方向。</summary>
        public static Vector2 RotateFromCanonicalRight(Vector2 canonicalOffset, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return canonicalOffset;
            }

            direction.Normalize();
            float angle = Mathf.Atan2(direction.y, direction.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            return new Vector2(
                canonicalOffset.x * cos - canonicalOffset.y * sin,
                canonicalOffset.x * sin + canonicalOffset.y * cos);
        }

        public static Vector3 ToVector3(Vector2 direction)
        {
            return new Vector3(direction.x, direction.y, 0f);
        }

        /// <summary>从棋盘槽位沿正交方向取邻格；越界或斜向返回 false。</summary>
        public static bool TryGetOrthogonalNeighbor(SlotId from, Vector2 direction, out SlotId to)
        {
            to = SlotId.None;
            if (!from.IsBoardSlot || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            int row = from.Row;
            int column = from.Column;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                column += direction.x > 0f ? 1 : -1;
            }
            else
            {
                row += direction.y > 0f ? -1 : 1;
            }

            if (row < 0 || row > 2 || column < 0 || column > 2)
            {
                return false;
            }

            to = SlotId.Board(row * 3 + column + 1);
            return true;
        }

        public static bool TryGetNeighborForDirection(SlotId from, CardBattleDirection direction, out SlotId to)
        {
            return TryGetOrthogonalNeighbor(from, ToVector2(direction), out to);
        }
    }
}
