using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 项目级卡面装配槽注册表：代号查找、角色标记、直接暴露子集。
    /// </summary>
    [CreateAssetMenu(
        fileName = "CardFaceSlotRegistry",
        menuName = "NineGrid/Cards/Card Face Slot Registry")]
    public sealed class CardFaceSlotRegistrySO : ScriptableObject
    {
        [SerializeField]
        [Tooltip("全部卡面装配槽；代号为键。留空条目会被忽略。")]
        private List<CardFaceSlotDefinition> slots = new List<CardFaceSlotDefinition>();

        private Dictionary<string, CardFaceSlotDefinition> _lookup;

        public IReadOnlyList<CardFaceSlotDefinition> Slots => slots;

        public bool TryGet(string code, out CardFaceSlotDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            EnsureLookup();
            return _lookup.TryGetValue(code, out definition) && definition != null;
        }

        public bool Contains(string code) => TryGet(code, out _);

        public void ReplaceSlots(IEnumerable<CardFaceSlotDefinition> definitions)
        {
            slots = new List<CardFaceSlotDefinition>();
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition == null || string.IsNullOrEmpty(definition.Code))
                    {
                        continue;
                    }

                    slots.Add(definition);
                }
            }

            _lookup = null;
        }

        /// <summary>
        /// 填入 Spec / ADR-0002 初版槽表（测试与资产创建共用）。
        /// </summary>
        public void ApplyDefaultCatalog()
        {
            ReplaceSlots(CreateDefaultDefinitions());
        }

        public static IReadOnlyList<CardFaceSlotDefinition> CreateDefaultDefinitions()
        {
            return new[]
            {
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.MainIcon,
                    "主图标",
                    CardFaceSlotRole.DirectExpose | CardFaceSlotRole.Icon | CardFaceSlotRole.InsertableInDescription),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.FaceBackground,
                    "卡面背景",
                    CardFaceSlotRole.DirectExpose | CardFaceSlotRole.Icon),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.BackBorder,
                    "卡背·背框",
                    CardFaceSlotRole.DirectExpose | CardFaceSlotRole.Icon),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.BackShirt,
                    "卡背·背纹",
                    CardFaceSlotRole.DirectExpose | CardFaceSlotRole.Icon),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.BackLogo,
                    "卡背·Logo",
                    CardFaceSlotRole.DirectExpose | CardFaceSlotRole.Icon),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.ActionIcon,
                    "行动图标",
                    CardFaceSlotRole.Collapsed | CardFaceSlotRole.Icon | CardFaceSlotRole.InsertableInDescription),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.Name,
                    "名字",
                    CardFaceSlotRole.Collapsed),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.Attack,
                    "攻击",
                    CardFaceSlotRole.Collapsed | CardFaceSlotRole.Numeric),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.Armor,
                    "护甲",
                    CardFaceSlotRole.Collapsed | CardFaceSlotRole.Numeric),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.Hp,
                    "血量",
                    CardFaceSlotRole.Collapsed | CardFaceSlotRole.Numeric),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.ActionCount,
                    "行动计数",
                    CardFaceSlotRole.Collapsed | CardFaceSlotRole.Numeric),
                CardFaceSlotDefinition.Create(
                    CardFaceSlotCodes.BasicDescription,
                    "基础描述",
                    CardFaceSlotRole.Collapsed),
            };
        }

        private void OnEnable()
        {
            _lookup = null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<string, CardFaceSlotDefinition>(StringComparer.Ordinal);
            if (slots == null)
            {
                slots = new List<CardFaceSlotDefinition>();
                return;
            }

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Code))
                {
                    continue;
                }

                _lookup[slot.Code] = slot;
            }
        }
    }
}
