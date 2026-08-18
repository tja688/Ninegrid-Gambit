using System;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Localization;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 简要解释 / 楼层提示文案纯逻辑（ADR-0020）。不碰 TMP、不复活旧 Description HUD。
    /// 玩家可见文案经 <see cref="L10n.Tr"/>（中文默认值内联，ADR-0046）。
    /// </summary>
    public static class BoardBriefTipCopy
    {
        public static string LeaveTip => L10n.Tr("briefTip.leave", "离开本房");
        public static string GoDownTip => L10n.Tr("briefTip.go_down", "前往下一层");
        public static string GoUpTip => L10n.Tr("briefTip.go_up", "返回上一层");

        /// <summary>属性房三选二完成 Notice（#137）：选满两张后经简要解释文字框播报。</summary>
        public static string AttributePickCompleteNotice =>
            L10n.Tr("notice.attribute_pick_complete", "已选择 2 张属性卡");

        /// <summary>
        /// 场地图标悬停文案：按房间类型输出「特色产物」描述（策划房间.md 口径，ADR-0020）。
        /// 困难房描述较长不带房名前缀；层主房用口号；无专属文案回退显示名。
        /// </summary>
        public static string ForRoom(RoomDefinition room)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.DisplayName))
            {
                return string.Empty;
            }

            switch (room.Kind)
            {
                case RoomKind.Attribute:
                    return L10n.Tr(
                        "briefTip.room_attribute",
                        "属性房：关卡开始时，从3张属性相关道具卡中选择2张（可重复）加入玩家侧卡组");
                case RoomKind.Gold:
                    return L10n.Tr(
                        "briefTip.room_gold",
                        "金币房：关卡开始时，将1张金币卡加入玩家侧卡组");
                case RoomKind.Treasure:
                    return L10n.Tr(
                        "briefTip.room_treasure",
                        "宝箱房：关卡开始时，将1张宝箱卡加入玩家侧卡组");
                case RoomKind.Fountain:
                    return L10n.Tr(
                        "briefTip.room_fountain",
                        "恢复房：关卡开始时，将1张食品卡加入玩家侧卡组");
                case RoomKind.Elite:
                    return L10n.Tr(
                        "briefTip.room_elite",
                        "关卡开始时，从特殊道具卡中随机选择2张加入玩家侧卡组，将两张强力怪物加入怪物侧卡组");
                case RoomKind.Boss:
                    return L10n.Tr("briefTip.room_boss", "准备迎接挑战了吗？");
                default:
                    return room.DisplayName;
            }
        }

        public static string ForNavigation(NavigationKind kind)
        {
            switch (kind)
            {
                case NavigationKind.Leave:
                    return LeaveTip;
                case NavigationKind.GoDown:
                    return GoDownTip;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 场地图标 contentId → 悬停文案。房间经 <paramref name="resolveRoom"/> 取 Catalog；
        /// 导航用固定文案（含本轮未启用的 GoUp）。
        /// </summary>
        public static string ForContentId(
            string contentId,
            Func<RoomKind, RoomDefinition> resolveRoom = null)
        {
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return string.Empty;
            }

            if (string.Equals(contentId, "GoUp", StringComparison.OrdinalIgnoreCase))
            {
                return GoUpTip;
            }

            if (Enum.TryParse(contentId, ignoreCase: true, out NavigationKind nav)
                && nav != NavigationKind.None)
            {
                return ForNavigation(nav);
            }

            if (!Enum.TryParse(contentId, ignoreCase: true, out RoomKind room)
                || room == RoomKind.None)
            {
                return contentId.Trim();
            }

            if (resolveRoom != null)
            {
                var def = resolveRoom(room);
                var composed = ForRoom(def);
                if (!string.IsNullOrEmpty(composed))
                {
                    return composed;
                }
            }

            return room.ToString();
        }

        /// <summary>
        /// 就地选项卡 / 商店货架文案（M2 接线时用）：简述 + 可选价格。
        /// </summary>
        public static string ForOptionOrShelf(string briefDescription, int? priceGold = null)
        {
            var brief = briefDescription == null ? string.Empty : briefDescription.Trim();
            if (priceGold.HasValue)
            {
                var price = string.Format(
                    L10n.Tr("briefTip.price_gold", "{0} 金币"),
                    priceGold.Value);
                if (string.IsNullOrEmpty(brief))
                {
                    return price;
                }

                return brief + " · " + price;
            }

            return brief;
        }

        /// <summary>大楼层提示：当前地下城环境显示名（ADR-0053）。</summary>
        public static string FormatFloorLevelHint(int floor, int nodeIndex, string difficultyId)
        {
            if (floor <= 0)
            {
                return string.Empty;
            }

            var environment = DungeonEnvironmentCatalog.ResolveFromRun(
                floor,
                nodeIndex,
                difficultyId ?? RunDifficultyIds.Default);
            return environment.DisplayName;
        }

        /// <summary>小房间提示：仅房间类型名，不含节点序号。</summary>
        public static string FormatRoomHint(RoomKind room)
        {
            switch (room)
            {
                case RoomKind.Elite:
                    return L10n.Tr("floor.room_elite", "精英战斗房间");
                case RoomKind.Boss:
                    return L10n.Tr("floor.room_boss", "Boss房间");
                case RoomKind.Shop:
                    return L10n.Tr("floor.room_shop", "商店房间");
                case RoomKind.Tavern:
                    return L10n.Tr("floor.room_tavern", "卡店房间");
                case RoomKind.Attribute:
                case RoomKind.Gold:
                case RoomKind.Fountain:
                case RoomKind.Treasure:
                    return L10n.Tr("floor.room_battle", "战斗房间");
                case RoomKind.TreasureReward:
                    return L10n.Tr("floor.room_treasure_reward", "宝箱奖励房间");
                case RoomKind.ItemReward:
                    return L10n.Tr("floor.room_item_reward", "道具奖励房间");
                default:
                    return string.Empty;
            }
        }
    }
}
