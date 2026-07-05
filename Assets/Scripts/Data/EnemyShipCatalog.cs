using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>敌舰数据条目（嵌入 EnemyShipCatalog 使用）。</summary>
    [Serializable]
    public sealed class EnemyShipDataEntry
    {
        [Header("基础信息")]
        [SerializeField] string enemyId;
        [SerializeField] string displayName;
        [SerializeField] string legacyName;
        [SerializeField] EnemyShipTier tier;
        [SerializeField] string routeNode = "1-1";

        [Header("数值")]
        [SerializeField] int armorValue = 200;

        [Header("炮火")]
        [SerializeField] EnemyArtilleryEntry[] artilleries = Array.Empty<EnemyArtilleryEntry>();

        [Header("表现")]
        [SerializeField, TextArea(2, 6)] string introDescription;
        [SerializeField] StripSpriteVisualCatalog visualCatalog;
        [SerializeField] string visualId = "spr_ship_1_strip9";

        public string EnemyId => enemyId;
        public string DisplayName => displayName;
        public string LegacyName => legacyName;
        public EnemyShipTier Tier => tier;
        public string RouteNode => routeNode;
        public int ArmorValue => armorValue;
        public EnemyArtilleryEntry[] Artilleries => artilleries;
        public string IntroDescription => introDescription;
        public StripSpriteVisualCatalog VisualCatalog => visualCatalog;
        public string VisualId => visualId;

        /// <summary>将目录中的动画条目应用到场景敌舰视觉组件。</summary>
        public void ApplyVisual(StripSpriteCharacterVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            if (visualCatalog != null)
            {
                visual.SetCatalog(visualCatalog);
            }

            if (!string.IsNullOrEmpty(visualId))
            {
                visual.SetVisual(visualId);
            }
        }

        /// <summary>敌人信息面板 hover 文案。</summary>
        public string BuildIntroText()
        {
            if (!string.IsNullOrWhiteSpace(introDescription))
            {
                return introDescription.Trim();
            }

            var sb = new StringBuilder();
            sb.Append(displayName);
            if (!string.IsNullOrEmpty(legacyName))
            {
                sb.Append("（前：").Append(legacyName).Append('）');
            }

            sb.Append("\n装甲值 ").Append(armorValue);

            if (artilleries != null)
            {
                for (var i = 0; i < artilleries.Length; i++)
                {
                    var entry = artilleries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.DisplayName))
                    {
                        continue;
                    }

                    sb.Append("\n· ").Append(entry.DisplayName);
                    if (!string.IsNullOrEmpty(entry.Description))
                    {
                        sb.Append('：').Append(entry.Description);
                    }
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>敌舰目录 —— 持有第一航段全部敌舰数据。</summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Enemy Ship Catalog", fileName = "EnemyShipCatalog")]
    public sealed class EnemyShipCatalog : ScriptableObject
    {
        [SerializeField] List<EnemyShipDataEntry> entries = new();
        public IReadOnlyList<EnemyShipDataEntry> Entries => entries;

        public EnemyShipDataEntry Get(string enemyId)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EnemyId == enemyId)
                {
                    return entries[i];
                }
            }

            return null;
        }

        public bool TryGet(string enemyId, out EnemyShipDataEntry entry)
        {
            entry = Get(enemyId);
            return entry != null;
        }

        public EnemyShipDataEntry GetByRouteNode(string routeNode)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].RouteNode == routeNode)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
