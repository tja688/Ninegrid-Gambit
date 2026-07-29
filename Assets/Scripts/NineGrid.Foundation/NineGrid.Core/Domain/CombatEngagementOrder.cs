using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 交战先手还击裁决：仅一方有先手还击时该方先；双方都有/都无时玩家先。
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
        /// 怪物是否应先于玩家出手（仅怪物有先手还击且玩家无）。
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
