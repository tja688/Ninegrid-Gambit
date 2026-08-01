namespace NineGrid.Core
{
    /// <summary>ADR-0017：可交战桶 Monster|Trap；真怪物桶仅 Monster。</summary>
    public static class CardCombatRules
    {
        public static bool IsBoardCombatTarget(CardKind kind) =>
            kind == CardKind.Monster || kind == CardKind.Trap;

        public static bool IsTrueMonster(CardKind kind) =>
            kind == CardKind.Monster;
    }
}
