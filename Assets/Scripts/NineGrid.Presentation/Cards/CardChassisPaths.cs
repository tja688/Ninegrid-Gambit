using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌底盘与卡面模板的权威资产路径（#13/#14；ADR-0017 含机关）。
    /// 运行时（Player）加载的 prefab / SO 一律位于 <c>Assets/Resources/</c> 下
    /// （ADR-0008 同源约定：Editor 走 AssetDatabase，Player 剥 /Resources/ 前缀走 Resources.Load），
    /// 保证打包后场地 / 检查面板 / 描述内联图标可见。
    /// Room / ChoiceOption 仅编辑器与后续场地投放使用，不进运行时五套 Spawn。
    /// </summary>
    public static class CardChassisPaths
    {
        public const string ChassisPrefab = "Assets/Resources/Prefabs/老Standard Card.prefab";
        public const string AvatarFacePrefab = "Assets/Resources/Prefabs/玩家卡标准模板.prefab";
        public const string MonsterFacePrefab = "Assets/Resources/Prefabs/怪物卡标准模板.prefab";
        public const string ItemFacePrefab = "Assets/Resources/Prefabs/道具卡标准模版.prefab";
        public const string RelicFacePrefab = "Assets/Resources/Prefabs/遗物卡标准模版.prefab";
        public const string TrapFacePrefab = "Assets/Resources/Prefabs/机关卡标准模版.prefab";
        public const string RoomOptionFacePrefab = "Assets/Resources/Prefabs/房间选项标准模板.prefab";

        /// <summary>遗物栏 HUD 图标显示壳（只负责图标+计数；命中 Collider 仍在 RelicSlot 锚点）。</summary>
        public const string RelicHudIconPrefab = "Assets/Resources/Prefabs/标准遗物图标模板.prefab";

        public const string RoomIconBattle = "Assets/Resources/Prefabs/地形图标/常规战斗图标.prefab";
        public const string RoomIconElite = "Assets/Resources/Prefabs/地形图标/困难战斗图标.prefab";
        public const string RoomIconBoss = "Assets/Resources/Prefabs/地形图标/Boss房图标.prefab";
        public const string RoomIconGold = "Assets/Resources/Prefabs/地形图标/钱袋图标.prefab";
        public const string RoomIconGoldAlt = "Assets/Resources/Prefabs/金钱图标.prefab";
        public const string RoomIconTreasure = "Assets/Resources/Prefabs/地形图标/宝箱图标.prefab";
        public const string RoomIconFountain = "Assets/Resources/Prefabs/地形图标/温泉图标.prefab";
        public const string RoomIconShop = "Assets/Resources/Prefabs/地形图标/商店图标.prefab";
        public const string RoomIconTavern = "Assets/Resources/Prefabs/地形图标/牌店图标.prefab";
        public const string RoomIconTavernLegacy = "Assets/Resources/Prefabs/地形图标/酒馆图标.prefab";
        public const string RoomIconAttribute = "Assets/Resources/Prefabs/地形图标/属性提升图标.prefab";
        public const string RoomIconItemReward = "Assets/Resources/Prefabs/地形图标/道具奖励图标.prefab";
        public const string RoomIconLeave = "Assets/Resources/Prefabs/地形图标/离开图标.prefab";
        public const string RoomIconGoUp = "Assets/Resources/Prefabs/地形图标/上楼图标.prefab";
        public const string RoomIconGoDown = "Assets/Resources/Prefabs/地形图标/下楼图标.prefab";

        public const string SlotRegistryAsset = "Assets/Arts/Cards/CardFaceSlotRegistry.asset";
        public const string DescriptionInlineIconStyleAsset =
            "Assets/Resources/Arts/Cards/CardFaceDescriptionInlineIconStyle.asset";
        public const string DescriptionIconCatalogAsset =
            "Assets/Resources/Arts/Cards/CardFaceDescriptionIconCatalog.asset";

        /// <summary>
        /// 怪物攻击模式槽图标图集（临时接线；完整节奏图标 Catalog 待后续）。
        /// </summary>
        public const string MonsterAttackPatternIconSheet =
            "Assets/Resources/ContentArt/Multiple/1786242411378_d.png";
        public const string GlossaryRowPrefab =
            "Assets/Resources/Prefabs/UI/词条详细效果信息.prefab";

        /// <summary>
        /// 按 Unity 资产路径加载 GameObject（prefab）。
        /// Editor / Play Mode in Editor 走 AssetDatabase；
        /// Player 剥 <c>/Resources/</c> 前缀走 Resources.Load（ADR-0008 同源约定）。
        /// </summary>
        public static GameObject LoadGameObject(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            path = path.Trim();
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            const string marker = "/Resources/";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var relative = path.Substring(index + marker.Length);
            var dot = relative.LastIndexOf('.');
            if (dot > 0)
            {
                relative = relative.Substring(0, dot);
            }

            return Resources.Load<GameObject>(relative);
#endif
        }

        /// <summary>按 Unity 资产路径加载 <typeparamref name="T"/>（SO/材质等；Player 剥 /Resources/ 前缀）。</summary>
        public static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            path = path.Trim();
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<T>(path);
#else
            const string marker = "/Resources/";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var relative = path.Substring(index + marker.Length);
            var dot = relative.LastIndexOf('.');
            if (dot > 0)
            {
                relative = relative.Substring(0, dot);
            }

            return Resources.Load<T>(relative);
#endif
        }

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
