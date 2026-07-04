using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>
    /// 船体改造稀有度
    /// </summary>
    public enum HullModRarity
    {
        Low,         // 低阶
        Mid,         // 中阶
        High,        // 高阶
        Privateer    // 私掠舰专属
    }

    /// <summary>
    /// 船体改造功能类型
    /// </summary>
    public enum HullModType
    {
        OrePoints,        // 矿石点数类
        ImpactBonus,      // 冲击加成型
        Economy,          // 经济类
        Operation,        // 运转类
        LongTermQuench,   // 长线淬火类
        Special,          // 特殊类
        Privateer         // 私掠舰专属
    }

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
    /// 通过 Create → NineGrid → Data → Hull Mod Catalog 创建。
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

    /// <summary>
    /// 单条船体改造 ScriptableObject —— 可在 Inspector 中独立引用。
    /// 通过 Create → NineGrid → Data → Hull Mod Data 创建。
    /// </summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Hull Mod Data", fileName = "HullModData")]
    public sealed class HullModDataSO : ScriptableObject
    {
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
}
