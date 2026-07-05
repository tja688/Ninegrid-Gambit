using System;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>
    /// 矿石矿阶 —— 决定基础点数与出现概率
    /// </summary>
    public enum OreTier
    {
        Crude,    // 粗矿 — 基础10点
        Refined,  // 精炼矿 — 基础20点
        PureGold  // 纯金矿 — 基础40点
    }

    /// <summary>
    /// 矿脉归属
    /// </summary>
    public enum OreVein
    {
        Universal,   // 通用矿脉
        Quench,      // 淬火矿脉
        Reforging,   // 重铸矿脉
        Derivative   // 衍生物
    }

    /// <summary>
    /// 矿石特性（Flags，一块矿石可携带多条特性）
    /// </summary>
    [Flags]
    public enum OreTrait
    {
        None      = 0,
        Ember     = 1 << 0,  // 余烬 — 回合结束后保留在精炼盘
        Quench    = 1 << 1,  // 淬火 — 摆下时永久+1点数
        Quench2   = 1 << 2,  // 淬火2 — 摆下时永久+2点数
        Station   = 1 << 3,  // 驻台 — 抛锚撞击后不移入矿渣堆
        Debris    = 1 << 4,  // 碎屑 — 摆下时生成0点矿渣
        Twin      = 1 << 5,  // 双晶 — 摆下时复制自身到精炼盘
        Preheat   = 1 << 6,  // 预热 — 下一块熔炼在本台的矿石获得半数点数
        Symbiosis = 1 << 7,  // 共生 — 摆下时抽一块矿石
        Symbiosis2= 1 << 8,  // 共生2 — 摆下时抽两块矿石
        Core      = 1 << 9,  // 熔核 — 摆下时点数翻倍
        Unity     = 1 << 10, // 齐心 — 需与另一块同时在场
        Sociable  = 1 << 11  // 合群 — 与多种矿石协同
    }

    /// <summary>
    /// 单块矿石 ScriptableObject —— 可在 Inspector 中独立引用。
    /// </summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Ore Data", fileName = "OreData")]
    public sealed class OreDataSO : ScriptableObject
    {
        [SerializeField] string oreId;
        [SerializeField] string displayName;
        [SerializeField] OreTier tier;
        [SerializeField] OreVein vein;

        [Header("数值")]
        [SerializeField] int basePoints;
        [SerializeField] int price;
        [SerializeField] OreTrait traits;

        [Header("表现")]
        [SerializeField, TextArea(1, 3)] string description;
        [SerializeField] Sprite icon;

        public string OreId       => oreId;
        public string DisplayName => displayName;
        public OreTier Tier       => tier;
        public OreVein Vein       => vein;
        public int BasePoints     => basePoints;
        public int Price          => price;
        public OreTrait Traits    => traits;
        public string Description => description;
        public Sprite Icon        => icon;
    }
}
