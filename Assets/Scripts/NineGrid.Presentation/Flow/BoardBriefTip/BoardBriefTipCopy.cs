using System;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Localization;
using NineGrid.Core.Systems;
using QFramework;

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

        /// <summary>
        /// 消费房/奖励房等离开图标悬停文案（前往当前层下半区节点 5）。
        /// 普通模式：进入密林_失落遗迹 / 进入岩层_熔岩之地 / 进入溶洞_黄昏礼堂；
        /// 困难模式（血色）：进入血色密林_失落遗迹 / 进入血色岩层_熔岩之地 / 进入血色溶洞_黄昏礼堂。
        /// </summary>
        public static string ForLeave(int floor, bool isHard)
        {
            var prefix = isHard
                ? L10n.Tr("briefTip.enter_blood_prefix", "进入血色")
                : L10n.Tr("briefTip.enter_prefix", "进入");
            switch (floor)
            {
                case 1:
                    return prefix + L10n.Tr("env.forest_lost_ruins", "密林_失落遗迹");
                case 2:
                    return prefix + L10n.Tr("env.rock_magma", "岩层_熔岩之地");
                case 3:
                    return prefix + L10n.Tr("env.cave_twilight_hall", "溶洞_黄昏礼堂");
                default:
                    return LeaveTip;
            }
        }

        public static string ForLeave(int floor, string difficultyId)
        {
            return ForLeave(floor, DungeonEnvironmentCatalog.IsHardDifficulty(difficultyId));
        }

        public static string ForLeave(IArchitecture arch)
        {
            var run = arch?.GetModel<RunModel>();
            var floor = run?.Floor?.Value ?? 1;
            var difficultyId = run?.DifficultyId?.Value;
            return ForLeave(floor, difficultyId);
        }

        /// <summary>
        /// Boss 战胜后前往下一层的悬停文案。
        /// 普通模式：进入下一层：岩层 / 进入下一层：溶洞；
        /// 困难模式（血色）：进入下一层：血色岩层 / 进入下一层：血色溶洞。
        /// </summary>
        public static string ForGoDown(int floor, bool isHard)
        {
            var prefix = L10n.Tr("briefTip.enter_next_floor_prefix", "进入下一层：");
            var blood = isHard ? L10n.Tr("briefTip.blood_layer_prefix", "血色") : string.Empty;
            switch (floor)
            {
                case 1:
                    return prefix + blood + L10n.Tr("layer.rock", "岩层");
                case 2:
                    return prefix + blood + L10n.Tr("layer.cave", "溶洞");
                default:
                    return GoDownTip;
            }
        }

        public static string ForGoDown(int floor, string difficultyId)
        {
            return ForGoDown(floor, DungeonEnvironmentCatalog.IsHardDifficulty(difficultyId));
        }

        public static string ForGoDown(IArchitecture arch)
        {
            var run = arch?.GetModel<RunModel>();
            var floor = run?.Floor?.Value ?? 1;
            var difficultyId = run?.DifficultyId?.Value;
            return ForGoDown(floor, difficultyId);
        }

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

        public static string ForNavigation(NavigationKind kind, IArchitecture arch = null)
        {
            switch (kind)
            {
                case NavigationKind.Leave:
                    return arch != null ? ForLeave(arch) : LeaveTip;
                case NavigationKind.GoDown:
                    return arch != null ? ForGoDown(arch) : GoDownTip;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 场地图标 contentId → 悬停文案。房间经 <paramref name="resolveRoom"/> 取 Catalog；
        /// 导航用固定或环境动态文案（含本轮未启用的 GoUp）。
        /// </summary>
        public static string ForContentId(
            string contentId,
            Func<RoomKind, RoomDefinition> resolveRoom = null,
            IArchitecture arch = null)
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
                return ForNavigation(nav, arch);
            }

            if (string.Equals(contentId, "Leave", StringComparison.OrdinalIgnoreCase))
            {
                return arch != null ? ForLeave(arch) : LeaveTip;
            }

            if (string.Equals(contentId, "GoDown", StringComparison.OrdinalIgnoreCase))
            {
                return arch != null ? ForGoDown(arch) : GoDownTip;
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
        /// 卡牌/货架/候选卡悬停文案（ADR-0020 / ADR-0035 / ADR-0037）：
        /// 统一解析卡牌名称 + 填充装配动态参数与强化增益（卡面同口径）+ 附带价格（可选）。
        /// 词条/图标副文本后续由 <see cref="BoardBriefTipPresenter"/> 渲染为卡面同款富文本与内联图标。
        /// </summary>
        public static string ForCard(string defId, IContentSystem content, int? priceGold = null)
        {
            if (string.IsNullOrWhiteSpace(defId))
            {
                return string.Empty;
            }

            string name = defId.Trim();
            string brief = string.Empty;

            if (content != null && content.HasCatalog
                && content.Catalog.Cards.TryGetValue(name, out var card)
                && card != null
                && !string.IsNullOrWhiteSpace(card.DisplayName))
            {
                name = card.DisplayName;
            }

            if (CardPresentationConfigCatalog.TryGet(name, out var dto)
                || CardPresentationConfigCatalog.TryGet(defId.Trim(), out dto))
            {
                if (dto != null)
                {
                    if (!string.IsNullOrWhiteSpace(dto.displayName))
                    {
                        name = dto.displayName;
                    }

                    if (!string.IsNullOrWhiteSpace(dto.description))
                    {
                        var kind = CoreCardPresentationMapper.ResolvePresentationKindFromDefId(defId);
                        if (kind == CardPresentationKind.HelpCard
                            || string.Equals(dto.kind, "HelpCard", StringComparison.OrdinalIgnoreCase))
                        {
                            brief = HelpCardMagnitudeOverlay.ProjectHelpCardDescription(
                                dto.description,
                                dto.effectAssemblies);
                        }
                        else
                        {
                            brief = CardFaceDescriptionProjector.Project(
                                CardDescriptionProjectionMode.Inspect,
                                dto.description,
                                dto.effectAssemblies);
                        }
                    }
                }
            }

            var body = string.IsNullOrWhiteSpace(brief) ? name : name + "：" + brief.Trim();
            return ForOptionOrShelf(body, priceGold);
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

        /// <summary>教程期间大楼层提示特化显示名。</summary>
        public static string TutorialFloorLevelHint => L10n.Tr("floor.tutorial_level", "密林_翡翠迷雾");

        /// <summary>教程期间小房间提示特化显示名。</summary>
        public static string TutorialRoomHint => L10n.Tr("floor.tutorial_room", "教程");
    }
}
