using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 交战先攻裁决：与设计文档「先攻」一致——仅一方有先攻时先攻方先；双方都有/都无时玩家先。
    /// </summary>
    public static class CombatEngagementOrder
    {
        public static bool HasFirstStrike(IStatSystem statSystem, CardInstance card)
        {
            if (statSystem == null || card == null)
            {
                return false;
            }

            return statSystem.EvaluateRule(RuleId.FirstStrike, 0f, statSystem.CreateContext(card)) > 0f;
        }

        /// <summary>
        /// 怪物是否应先于玩家出手（仅怪物有先攻且玩家无先攻）。
        /// </summary>
        public static bool MonsterStrikesFirst(
            IStatSystem statSystem,
            CardInstance avatar,
            CardInstance monster)
        {
            if (statSystem == null || avatar == null || monster == null)
            {
                return false;
            }

            return HasFirstStrike(statSystem, monster) && !HasFirstStrike(statSystem, avatar);
        }
    }
}
