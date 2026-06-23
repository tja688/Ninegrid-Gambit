using UnityEngine;

namespace NineGrid.Presentation.Performance
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
    }
}
