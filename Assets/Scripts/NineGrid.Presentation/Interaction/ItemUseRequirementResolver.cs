using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 根据帮助卡 defId 解析道具使用前置条件（直发 / 选项 / 棋盘目标）。
    /// </summary>
    public static class ItemUseRequirementResolver
    {
        private static readonly HashSet<string> MonsterTargetDefIds = new()
        {
            "help.throwing_knife",
            "help.fireball",
            "help.impact_tutorial",
            "help.shield_bash_tutorial",
            "help.armor_breaking_hammer",
        };

        public static ItemUseRequirement Resolve(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return ItemUseRequirement.Direct;
            }

            if (defId == "help.stat_boost_card")
            {
                return ItemUseRequirement.StatBoostOptions();
            }

            if (defId == "help.swap_card")
            {
                return ItemUseRequirement.BoardTargets(2);
            }

            if (defId == "help.teleport_card")
            {
                return ItemUseRequirement.BoardTargets(1);
            }

            if (defId == "help.kidnapping")
            {
                return ItemUseRequirement.BoardTargets(1, CardKind.Monster, excludeElite: true, excludeBoss: true);
            }

            if (MonsterTargetDefIds.Contains(defId))
            {
                return ItemUseRequirement.BoardTargets(1, CardKind.Monster);
            }

            return ItemUseRequirement.Direct;
        }
    }
}
