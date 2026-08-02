using System;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 简要解释 / 楼层提示文案纯逻辑（ADR-0020）。不碰 TMP、不复活旧 Description HUD。
    /// </summary>
    public static class BoardBriefTipCopy
    {
        public const string LeaveTip = "离开本房";
        public const string GoDownTip = "前往下一层";
        public const string GoUpTip = "返回上一层";

        public static string ForRoom(RoomDefinition room)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.DisplayName))
            {
                return string.Empty;
            }

            if (room.OpeningInjects != null && room.OpeningInjects.Count > 0)
            {
                return room.DisplayName + "：开局注入 " + room.OpeningInjects.Count + " 项";
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
                if (string.IsNullOrEmpty(brief))
                {
                    return priceGold.Value + " 金币";
                }

                return brief + " · " + priceGold.Value + " 金币";
            }

            return brief;
        }

        public static string FormatFloorHint(int floor, int nodeIndex)
        {
            return "第 " + floor + " 层 · 节点 " + MapNodeProgression.ToDisplayNode(nodeIndex);
        }
    }
}
