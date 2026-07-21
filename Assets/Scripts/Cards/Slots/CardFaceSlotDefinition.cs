using System;
using UnityEngine;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 装配槽注册表中的单条契约：代号为键，中文注释供 Inspector 悬停。
    /// </summary>
    [Serializable]
    public sealed class CardFaceSlotDefinition
    {
        [SerializeField]
        [Tooltip("槽代号（代码内部键），例如 Main_Icon。不可用中文当主键。")]
        private string code = string.Empty;

        [SerializeField]
        [Tooltip("编辑器悬停中文注释，例如「主图标」。")]
        private string displayNameZh = string.Empty;

        [SerializeField]
        [Tooltip("槽角色：直接暴露 / 收纳 / 可插入描述 / 数值 / 图标等，可组合。")]
        private CardFaceSlotRole roles = CardFaceSlotRole.Collapsed;

        public string Code => code;
        public string DisplayNameZh => displayNameZh;
        public CardFaceSlotRole Roles => roles;

        public bool HasRole(CardFaceSlotRole role) => (roles & role) != 0;

        public static CardFaceSlotDefinition Create(string slotCode, string nameZh, CardFaceSlotRole slotRoles)
        {
            return new CardFaceSlotDefinition
            {
                code = slotCode ?? string.Empty,
                displayNameZh = nameZh ?? string.Empty,
                roles = slotRoles
            };
        }
    }
}
