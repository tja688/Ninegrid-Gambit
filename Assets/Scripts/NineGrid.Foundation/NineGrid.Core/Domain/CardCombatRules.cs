namespace NineGrid.Core
{
    /// <summary>ADR-0017：可交战桶 Monster|Trap；真怪物桶仅 Monster。</summary>
    public static class CardCombatRules
    {
        public static bool IsBoardCombatTarget(CardKind kind) =>
            kind == CardKind.Monster || kind == CardKind.Trap;

        public static bool IsTrueMonster(CardKind kind) =>
            kind == CardKind.Monster;

        /// <summary>
        /// SelectedCards 等选牌过滤：<c>kind=Monster</c> 默认走可交战桶；
        /// <c>trueMonsterOnly</c> 时仅真怪（绑架等）。其它 Kind 精确匹配。
        /// </summary>
        public static bool MatchesSelectedKindFilter(CardKind cardKind, CardKind filterKind, bool trueMonsterOnly)
        {
            if (filterKind == CardKind.Unknown)
            {
                return true;
            }

            if (filterKind == CardKind.Monster)
            {
                return trueMonsterOnly ? IsTrueMonster(cardKind) : IsBoardCombatTarget(cardKind);
            }

            return cardKind == filterKind;
        }
    }
}
