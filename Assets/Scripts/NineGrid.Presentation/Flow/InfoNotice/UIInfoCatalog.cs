using System;
using System.Collections.Generic;
using NineGrid.Core.Localization;
using UnityEngine;

namespace NineGrid.Flow.InfoNotice
{
    /// <summary>
    /// UI 判定框（Tag: UIInfo）描述信息配置表。
    /// 便于集中维护与修改各 UI 模块的简要介绍文案。
    /// </summary>
    public static class UIInfoCatalog
    {
        public const string TagName = "UIInfo";

        public const string KeyHpBar = "血条";
        public const string KeyBaseArmor = "基础护甲";
        public const string KeyGold = "金币";
        public const string KeyRelicPanel = "RelicPanel";
        public const string KeyCardDeckAnchors = "CardDeckAnchors";

        private static readonly Dictionary<string, string> sDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { KeyHpBar, "这是你的血量，归零即为死亡，" },
            { "PlayerInfoPanel", "这是你的血量，归零即为死亡，" },
            { "bloodBarRoot", "这是你的血量，归零即为死亡，" },

            { KeyBaseArmor, "每场对战开始时，基础护甲会给你提供相同数值的当前护甲" },

            { KeyGold, "获得的金币可以在商店或者卡店进行消费" },

            { KeyRelicPanel, "遗物可以通过拖拽到回收区进行回收，右键点击遗物查看详情" },
            { "遗物", "遗物可以通过拖拽到回收区进行回收，右键点击遗物查看详情" },

            { KeyCardDeckAnchors, "卡组的顶部卡牌必然在下一张进入场地，拖拽遗物或手牌可以在这里回收" },
            { "卡组", "卡组的顶部卡牌必然在下一张进入场地，拖拽遗物或手牌可以在这里回收" },
        };

        /// <summary>
        /// 获取指定键的描述文案。
        /// </summary>
        public static bool TryGetDescription(string key, out string description)
        {
            description = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (sDescriptions.TryGetValue(key.Trim(), out var text) && !string.IsNullOrEmpty(text))
            {
                description = L10n.Tr("uiinfo." + key.Trim(), text);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 依据命中物体或其层级结构解析对应的 UI 描述文案。
        /// </summary>
        public static bool TryResolveDescription(GameObject go, out string description)
        {
            description = null;
            if (go == null)
            {
                return false;
            }

            // 1. 尝试物体自身名称
            if (TryGetDescription(go.name, out description))
            {
                return true;
            }

            // 2. 尝试向上遍历父级名称
            var current = go.transform.parent;
            while (current != null)
            {
                if (TryGetDescription(current.name, out description))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// 注册或覆盖描述文案（便于运行时定制或测试注入）。
        /// </summary>
        public static void SetDescription(string key, string description)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            sDescriptions[key.Trim()] = description;
        }
    }
}
