using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>
    /// 单条船体改造数据（嵌入 HullModCatalog 使用）
    /// </summary>
    [Serializable]
    public sealed class HullModDataEntry
    {
        [Header("基础信息")]
        [SerializeField] string modId;
        [SerializeField] string displayName;
        [SerializeField] HullModRarity rarity;
        [SerializeField] HullModType modType;

        [Header("数值")]
        [SerializeField] int price;

        [Header("表现")]
        [SerializeField, TextArea(1, 3)] string effectDescription;
        [SerializeField] Sprite icon;

        public string ModId             => modId;
        public string DisplayName       => displayName;
        public HullModRarity Rarity     => rarity;
        public HullModType ModType      => modType;
        public int Price                => price;
        public string EffectDescription => effectDescription;
        public Sprite Icon              => icon;
    }

    /// <summary>
    /// 船体改造目录 —— 持有全部改造数据条目。
    /// </summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Hull Mod Catalog", fileName = "HullModCatalog")]
    public sealed class HullModCatalog : ScriptableObject
    {
        [SerializeField] List<HullModDataEntry> entries = new();
        public IReadOnlyList<HullModDataEntry> Entries => entries;

        public HullModDataEntry Get(string modId)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].ModId == modId) return entries[i];
            return null;
        }

        public bool TryGet(string modId, out HullModDataEntry entry)
        {
            entry = Get(modId);
            return entry != null;
        }
    }
}
