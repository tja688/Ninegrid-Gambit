using System;
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
    /// 单条船体改造 ScriptableObject —— 可在 Inspector 中独立引用。
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
