using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌底盘与卡面模板的权威资产路径（#13/#14；ADR-0017 含机关）。
    /// 旧路径 Assets/Prefabs/Standard Card.prefab 已失效，一律改用本常量。
    /// Room / ChoiceOption 仅编辑器与后续场地投放使用，不进运行时五套 Spawn。
    /// </summary>
    public static class CardChassisPaths
    {
        public const string ChassisPrefab = "Assets/Prefabs/老Standard Card.prefab";
        public const string AvatarFacePrefab = "Assets/Prefabs/玩家卡标准模板.prefab";
        public const string MonsterFacePrefab = "Assets/Prefabs/怪物卡标准模板.prefab";
        public const string ItemFacePrefab = "Assets/Prefabs/道具卡标准模版.prefab";
        public const string RelicFacePrefab = "Assets/Prefabs/遗物卡标准模版.prefab";
        public const string TrapFacePrefab = "Assets/Prefabs/机关卡标准模版.prefab";
        public const string RoomOptionFacePrefab = "Assets/Prefabs/房间选项标准模板.prefab";

        public const string RoomIconBattle = "Assets/Prefabs/地形图标/常规战斗图标.prefab";
        public const string RoomIconElite = "Assets/Prefabs/地形图标/困难战斗图标.prefab";
        public const string RoomIconBoss = "Assets/Prefabs/地形图标/Boss房图标.prefab";
        public const string RoomIconGold = "Assets/Prefabs/地形图标/钱袋图标.prefab";
        public const string RoomIconGoldAlt = "Assets/Prefabs/金钱图标.prefab";
        public const string RoomIconTreasure = "Assets/Prefabs/地形图标/宝箱图标.prefab";
        public const string RoomIconFountain = "Assets/Prefabs/地形图标/温泉图标.prefab";
        public const string RoomIconShop = "Assets/Prefabs/地形图标/商店图标.prefab";
        public const string RoomIconTavern = "Assets/Prefabs/地形图标/牌店图标.prefab";
        public const string RoomIconTavernLegacy = "Assets/Prefabs/地形图标/酒馆图标.prefab";
        public const string RoomIconAttribute = "Assets/Prefabs/地形图标/属性提升图标.prefab";
        public const string RoomIconItemReward = "Assets/Prefabs/地形图标/道具奖励图标.prefab";
        public const string RoomIconLeave = "Assets/Prefabs/地形图标/离开图标.prefab";
        public const string RoomIconGoUp = "Assets/Prefabs/地形图标/上楼图标.prefab";
        public const string RoomIconGoDown = "Assets/Prefabs/地形图标/下楼图标.prefab";

        public const string SlotRegistryAsset = "Assets/Arts/Cards/CardFaceSlotRegistry.asset";
        public const string DescriptionInlineIconStyleAsset =
            "Assets/Arts/Cards/CardFaceDescriptionInlineIconStyle.asset";
        public const string DescriptionIconCatalogAsset =
            "Assets/Arts/Cards/CardFaceDescriptionIconCatalog.asset";

        /// <summary>
        /// 解析房间图标预制体：优先 JSON <paramref name="iconPrefabOverride"/>，否则按 contentId/RoomKind 默认表。
        /// </summary>
        public static string ResolveRoomIconPrefab(string contentId, string iconPrefabOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(iconPrefabOverride))
            {
                return iconPrefabOverride.Trim();
            }

            if (string.IsNullOrWhiteSpace(contentId))
            {
                return RoomIconBattle;
            }

            switch (contentId.Trim())
            {
                case "Elite":
                    return RoomIconElite;
                case "Boss":
                    return RoomIconBoss;
                case "Gold":
                    return RoomIconGold;
                case "Treasure":
                case "TreasureReward":
                    return RoomIconTreasure;
                case "Fountain":
                    return RoomIconFountain;
                case "Shop":
                    return RoomIconShop;
                case "Tavern":
                    return RoomIconTavern;
                case "Attribute":
                    return RoomIconAttribute;
                case "ItemReward":
                    return RoomIconItemReward;
                case "Leave":
                    return RoomIconLeave;
                case "GoUp":
                    return RoomIconGoUp;
                case "GoDown":
                    return RoomIconGoDown;
                default:
                    return RoomIconBattle;
            }
        }

        public static bool IsRoomOrChoiceOptionKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            return string.Equals(kind, "Room", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(kind, "ChoiceOption", StringComparison.OrdinalIgnoreCase);
        }
    }
}
