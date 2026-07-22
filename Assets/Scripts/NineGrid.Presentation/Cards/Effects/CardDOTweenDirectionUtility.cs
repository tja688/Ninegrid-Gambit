using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 内化 Tween 片段的左右朝向解析。
    /// </summary>
    public static class CardDOTweenDirectionUtility
    {
        public static float ResolveHorizontalSign(CardBoardDirection direction)
        {
            return direction switch
            {
                CardBoardDirection.Left => -1f,
                CardBoardDirection.UpLeft => -1f,
                CardBoardDirection.DownLeft => -1f,
                CardBoardDirection.Right => 1f,
                CardBoardDirection.UpRight => 1f,
                CardBoardDirection.DownRight => 1f,
                _ => 1f,
            };
        }

        public static Vector3 ApplyHorizontalSign(Vector3 value, CardBoardDirection direction)
        {
            var sign = ResolveHorizontalSign(direction);
            if (Mathf.Approximately(sign, 1f))
            {
                return value;
            }

            value.x *= sign;
            return value;
        }
    }
}
