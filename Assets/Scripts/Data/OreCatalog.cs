using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>
    /// 单块矿石的运行时数据（嵌入 OreCatalog 使用）
    /// </summary>
    [Serializable]
    public sealed class OreDataEntry
    {
        [Header("基础信息")]
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

    /// <summary>
    /// 矿石目录 —— 持有全部矿石数据条目。
    /// </summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Ore Catalog", fileName = "OreCatalog")]
    public sealed class OreCatalog : ScriptableObject
    {
        [SerializeField] List<OreDataEntry> entries = new();
        public IReadOnlyList<OreDataEntry> Entries => entries;

        public OreDataEntry Get(string oreId)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].OreId == oreId) return entries[i];
            return null;
        }

        public bool TryGet(string oreId, out OreDataEntry entry)
        {
            entry = Get(oreId);
            return entry != null;
        }
    }
}
