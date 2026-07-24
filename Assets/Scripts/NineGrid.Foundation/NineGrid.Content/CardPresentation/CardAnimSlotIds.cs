using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌主视图动画槽代号（JSON / 运行时共用）。
    /// lunch = 可选特殊态（特殊攻击/施法等）；缺失时回退 idle → 静态 mainIcon。
    /// </summary>
    public static class CardAnimSlotIds
    {
        public const string Idle = "idle";
        public const string Attack = "attack";
        public const string Hurt = "hurt";
        public const string Death = "death";
        public const string Lunch = "lunch";

        public static readonly string[] All =
        {
            Idle,
            Attack,
            Hurt,
            Death,
            Lunch,
        };

        /// <summary>
        /// 规范化槽名（小写 trim）；未知或空 → idle。
        /// </summary>
        public static string Normalize(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot))
            {
                return Idle;
            }

            var trimmed = slot.Trim();
            for (var i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return All[i];
                }
            }

            return Idle;
        }
    }
}
