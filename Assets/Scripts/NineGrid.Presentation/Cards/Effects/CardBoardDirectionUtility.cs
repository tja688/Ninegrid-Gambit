using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 八向方向工具：可选从格位推算本地方向，或转换为局部偏移向量。
    /// </summary>
    public static class CardBoardDirectionUtility
    {
        /// <summary>
        /// 基于九宫格行列差，计算 observer 相对 focus 的本地方向（供简单场景复用）。
        /// </summary>
        public static CardBoardDirection ComputeSelfDirection(int observerSlot, int focusSlot)
        {
            if (!GroundSlotTopology.IsValidSlot(observerSlot) || !GroundSlotTopology.IsValidSlot(focusSlot))
            {
                return CardBoardDirection.None;
            }

            if (observerSlot == focusSlot)
            {
                return CardBoardDirection.None;
            }

            var rowDelta = GroundSlotTopology.GetRow(observerSlot) - GroundSlotTopology.GetRow(focusSlot);
            var colDelta = GroundSlotTopology.GetColumn(observerSlot) - GroundSlotTopology.GetColumn(focusSlot);

            return FromRowColDelta(rowDelta, colDelta);
        }

        /// <summary>
        /// 正交四向取反（用于怪物反击：攻击格与玩家格互换后选用反向 rig）。
        /// </summary>
        public static CardBoardDirection GetOrthogonalOpposite(CardBoardDirection direction)
        {
            return direction switch
            {
                CardBoardDirection.Up => CardBoardDirection.Down,
                CardBoardDirection.Down => CardBoardDirection.Up,
                CardBoardDirection.Left => CardBoardDirection.Right,
                CardBoardDirection.Right => CardBoardDirection.Left,
                _ => CardBoardDirection.None,
            };
        }

        /// <summary>
        /// 将八向转换为局部 XY 偏移（Y 向上为正），长度为 distance。
        /// </summary>
        public static Vector3 ToLocalOffset(CardBoardDirection direction, float distance)
        {
            if (direction == CardBoardDirection.None || distance <= 0f)
            {
                return Vector3.zero;
            }

            var offset = direction switch
            {
                CardBoardDirection.Up => new Vector2(0f, 1f),
                CardBoardDirection.Down => new Vector2(0f, -1f),
                CardBoardDirection.Left => new Vector2(-1f, 0f),
                CardBoardDirection.Right => new Vector2(1f, 0f),
                CardBoardDirection.UpLeft => new Vector2(-1f, 1f),
                CardBoardDirection.UpRight => new Vector2(1f, 1f),
                CardBoardDirection.DownLeft => new Vector2(-1f, -1f),
                CardBoardDirection.DownRight => new Vector2(1f, -1f),
                _ => Vector2.zero,
            };

            if (offset.sqrMagnitude > 1f)
            {
                offset.Normalize();
            }

            return new Vector3(offset.x, offset.y, 0f) * distance;
        }

        private static CardBoardDirection FromRowColDelta(int rowDelta, int colDelta)
        {
            if (rowDelta == 0 && colDelta == 0)
            {
                return CardBoardDirection.None;
            }

            var vertical = rowDelta < 0 ? CardBoardDirection.Up
                : rowDelta > 0 ? CardBoardDirection.Down
                : CardBoardDirection.None;

            var horizontal = colDelta < 0 ? CardBoardDirection.Left
                : colDelta > 0 ? CardBoardDirection.Right
                : CardBoardDirection.None;

            return (vertical, horizontal) switch
            {
                (CardBoardDirection.Up, CardBoardDirection.Left) => CardBoardDirection.UpLeft,
                (CardBoardDirection.Up, CardBoardDirection.Right) => CardBoardDirection.UpRight,
                (CardBoardDirection.Down, CardBoardDirection.Left) => CardBoardDirection.DownLeft,
                (CardBoardDirection.Down, CardBoardDirection.Right) => CardBoardDirection.DownRight,
                (CardBoardDirection.Up, CardBoardDirection.None) => CardBoardDirection.Up,
                (CardBoardDirection.Down, CardBoardDirection.None) => CardBoardDirection.Down,
                (CardBoardDirection.None, CardBoardDirection.Left) => CardBoardDirection.Left,
                (CardBoardDirection.None, CardBoardDirection.Right) => CardBoardDirection.Right,
                _ => CardBoardDirection.None,
            };
        }
    }
}
