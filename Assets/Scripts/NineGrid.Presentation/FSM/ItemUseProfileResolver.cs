using NineGrid.Core;

namespace NineGrid.Presentation.FSM
{
    public enum ItemUseProfileMode
    {
        ApplyZone,
        SingleTarget,
        MultiPick,
        OptionOverlay,
    }

    public struct ItemUseProfile
    {
        public ItemUseProfileMode Mode;
        public CardKind AllowedKind;
        public int RequiredCount;
        public bool ExcludeElite;
        public bool ExcludeBoss;
    }

    /// <summary>
    /// 道具使用 Profile 首版硬编码（临时路由：ApplyZone 释放区 + 点选目标）。
    /// 完整多态路由待 ItemTargetingSession 落地后替换。
    /// </summary>
    public static class ItemUseProfileResolver
    {
        public static ItemUseProfile Resolve(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return ApplyZoneDefault();
            }

            switch (defId)
            {
                case "help.throwing_knife":
                case "help.fireball":
                case "help.impact_tutorial":
                case "help.shield_bash_tutorial":
                case "help.armor_breaking_hammer":
                    return new ItemUseProfile
                    {
                        Mode = ItemUseProfileMode.SingleTarget,
                        AllowedKind = CardKind.Monster,
                        RequiredCount = 1,
                    };

                case "help.kidnapping":
                    return new ItemUseProfile
                    {
                        Mode = ItemUseProfileMode.SingleTarget,
                        AllowedKind = CardKind.Monster,
                        RequiredCount = 1,
                        ExcludeElite = true,
                        ExcludeBoss = true,
                    };

                case "help.teleport_card":
                    return new ItemUseProfile
                    {
                        Mode = ItemUseProfileMode.SingleTarget,
                        AllowedKind = CardKind.Unknown,
                        RequiredCount = 1,
                    };

                case "help.swap_card":
                    return new ItemUseProfile
                    {
                        Mode = ItemUseProfileMode.MultiPick,
                        AllowedKind = CardKind.Unknown,
                        RequiredCount = 2,
                    };

                case "help.stat_boost_card":
                    return new ItemUseProfile
                    {
                        Mode = ItemUseProfileMode.OptionOverlay,
                        RequiredCount = 0,
                    };

                default:
                    return ApplyZoneDefault();
            }
        }

        private static ItemUseProfile ApplyZoneDefault()
        {
            return new ItemUseProfile
            {
                Mode = ItemUseProfileMode.ApplyZone,
                RequiredCount = 0,
            };
        }
    }
}
