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

        public static string ForRoom(RoomDefinition room)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.DisplayName))
            {
                return string.Empty;
            }

            if (room.OpeningInjects != null && room.OpeningInjects.Count > 0)
            {
                return string.Format(
                    L10n.Tr("briefTip.opening_inject", "{0}：开局注入 {1} 项"),
                    room.DisplayName,
                    room.OpeningInjects.Count);
            }

            return room.DisplayName;
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

        /// <summary>大楼层提示：仅楼层，拉丁数字，如「楼层·Ⅱ」/「Floor II」。</summary>
        public static string FormatFloorLevelHint(int floor)
        {
            if (floor <= 0)
            {
                return string.Empty;
            }

            return string.Format(
                L10n.Tr("floor.level", "楼层·{0}"),
                ToLatinNumeral(floor));
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

        private static string ToLatinNumeral(int value)
        {
            switch (value)
            {
                case 1: return "Ⅰ";
                case 2: return "Ⅱ";
                case 3: return "Ⅲ";
                case 4: return "Ⅳ";
                case 5: return "Ⅴ";
                case 6: return "Ⅵ";
                case 7: return "Ⅶ";
                case 8: return "Ⅷ";
                case 9: return "Ⅸ";
                case 10: return "Ⅹ";
                default: return value.ToString();
            }
        }
    }
}
