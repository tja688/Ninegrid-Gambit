namespace NineGrid.Core
{
    /// <summary>
    /// 怪物攻击模式（ADR-0011）。内生必填属性，不进效果 DSL。
    /// <see cref="Unspecified"/> 仅表示缺省/非法，加载期须报错，不得静默当 <see cref="None"/>。
    /// </summary>
    public enum AttackPattern
    {
        Unspecified = 0,
        /// <summary>无 — 显式正当取值，不开火。</summary>
        None = 1,
        /// <summary>普通近战 — 正交相邻。</summary>
        OrthogonalMelee = 2,
        /// <summary>斜角近战 — 对角相邻。</summary>
        DiagonalMelee = 3,
        /// <summary>全向近战 — 八向相邻。</summary>
        OmnidirectionalMelee = 4,
    }

    /// <summary>攻击模式解析与几何（ADR-0011；频率表已由 ADR-0038 废止）。</summary>
    public static class AttackPatternRules
    {
        public const string TokenNone = "无";
        public const string TokenOrthogonalMelee = "普通近战";
        public const string TokenDiagonalMelee = "斜角近战";
        public const string TokenOmnidirectionalMelee = "全向近战";

        /// <summary>
        /// 已废止：节奏周期只读卡级 <c>RhythmPeriod</c>（ADR-0038）。
        /// 保留方法以免旧调用方编译失败；恒返回 0。
        /// </summary>
        public static int Frequency(AttackPattern pattern)
        {
            _ = pattern;
            return 0;
        }

        public static bool TryParse(string raw, out AttackPattern pattern)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                pattern = AttackPattern.Unspecified;
                return false;
            }

            switch (raw.Trim())
            {
                case TokenNone:
                    pattern = AttackPattern.None;
                    return true;
                case TokenOrthogonalMelee:
                    pattern = AttackPattern.OrthogonalMelee;
                    return true;
                case TokenDiagonalMelee:
                    pattern = AttackPattern.DiagonalMelee;
                    return true;
                case TokenOmnidirectionalMelee:
                    pattern = AttackPattern.OmnidirectionalMelee;
                    return true;
                default:
                    pattern = AttackPattern.Unspecified;
                    return false;
            }
        }

        /// <summary>非「无」/缺省即可在开火窗口尝试单向打击（几何资格另判）。</summary>
        public static bool ParticipatesInEnemyAction(AttackPattern pattern)
        {
            return pattern != AttackPattern.None && pattern != AttackPattern.Unspecified;
        }

        /// <summary>开火位置条件（ADR-0011）。由敌方行动阶段资格复核消费（#79/#80）。</summary>
        public static bool MeetsPositionRequirement(AttackPattern pattern, SlotId monsterSlot, SlotId avatarSlot)
        {
            if (!monsterSlot.IsBoardSlot || !avatarSlot.IsBoardSlot)
            {
                return false;
            }

            switch (pattern)
            {
                case AttackPattern.OrthogonalMelee:
                    return monsterSlot.IsAdjacentTo(avatarSlot);
                case AttackPattern.DiagonalMelee:
                    return monsterSlot.IsDiagonallyAdjacentTo(avatarSlot);
                case AttackPattern.OmnidirectionalMelee:
                    return monsterSlot.IsAdjacentTo(avatarSlot)
                        || monsterSlot.IsDiagonallyAdjacentTo(avatarSlot);
                default:
                    return false;
            }
        }
    }
}
