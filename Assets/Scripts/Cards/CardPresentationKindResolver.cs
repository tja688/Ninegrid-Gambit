using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards 程序集内的 Kind 解析（不引用 Core/Flow）。正式路径优先由 Flow 传入 Core Kind。
    /// skill.* 映射为 Unknown：不为 PlayerSkill 开卡面。
    /// </summary>
    public static class CardPresentationKindResolver
    {
        public static CardPresentationKind FromDefId(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return CardPresentationKind.Unknown;
            }

            if (defId.StartsWith("monster.", StringComparison.Ordinal))
            {
                return CardPresentationKind.Monster;
            }

            if (defId.StartsWith("help.", StringComparison.Ordinal)
                || defId.StartsWith("player.", StringComparison.Ordinal))
            {
                return CardPresentationKind.HelpCard;
            }

            if (defId.StartsWith("relic.", StringComparison.Ordinal))
            {
                return CardPresentationKind.Relic;
            }

            if (defId.StartsWith("avatar.", StringComparison.Ordinal))
            {
                return CardPresentationKind.Avatar;
            }

            if (defId.StartsWith("item.", StringComparison.Ordinal))
            {
                return CardPresentationKind.Item;
            }

            return CardPresentationKind.Unknown;
        }
    }
}
