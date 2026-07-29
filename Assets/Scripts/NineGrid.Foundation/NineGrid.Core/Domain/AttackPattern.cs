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
        /// <summary>普通近战 — 正交相邻，频率 3。</summary>
        OrthogonalMelee = 2,
        /// <summary>斜角近战 — 对角相邻，频率 3。</summary>
        DiagonalMelee = 3,
        /// <summary>全向近战 — 八向相邻，频率 3。</summary>
        OmnidirectionalMelee = 4,
        /// <summary>普通远程 — 无位置条件，频率 5。</summary>
        Ranged = 5,
    }

    /// <summary>攻击模式解析与频率表（ADR-0011）。</summary>
    public static class AttackPatternRules
    {
        public const string TokenNone = "无";
        public const string TokenOrthogonalMelee = "普通近战";
        public const string TokenDiagonalMelee = "斜角近战";
        public const string TokenOmnidirectionalMelee = "全向近战";
        public const string TokenRanged = "普通远程";

        public static int Frequency(AttackPattern pattern)
        {
            switch (pattern)
            {
                case AttackPattern.OrthogonalMelee:
                case AttackPattern.DiagonalMelee:
                case AttackPattern.OmnidirectionalMelee:
                    return 3;
                case AttackPattern.Ranged:
                    return 5;
                default:
                    return 0;
            }
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
                case TokenRanged:
                    pattern = AttackPattern.Ranged;
                    return true;
                default:
                    pattern = AttackPattern.Unspecified;
                    return false;
            }
        }

        /// <summary>非「无」/缺省即会参与行动倒计时推进。</summary>
        public static bool ParticipatesInEnemyAction(AttackPattern pattern)
        {
            return pattern != AttackPattern.None && pattern != AttackPattern.Unspecified;
        }

        /// <summary>开火位置条件（ADR-0011）。#79 主测普通近战；其余模式谓词已就绪供 #80。</summary>
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
                case AttackPattern.Ranged:
                    return true;
                default:
                    return false;
            }
        }
    }
}
