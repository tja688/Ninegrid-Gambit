using System;
using System.Text;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Data
{
    /// <summary>敌舰分类</summary>
    public enum EnemyShipTier
    {
        Normal,     // 普通
        Privateer,  // 私掠舰（精英）
        Flagship    // 旗舰（BOSS）
    }

    /// <summary>单条炮火配置（机制落地前以文案描述为主）。</summary>
    [Serializable]
    public sealed class EnemyArtilleryEntry
    {
        [SerializeField] string artilleryId;
        [SerializeField] string displayName;
        [SerializeField, TextArea(1, 3)] string description;

        public string ArtilleryId => artilleryId;
        public string DisplayName => displayName;
        public string Description => description;
    }

    /// <summary>
    /// 单艘敌舰 ScriptableObject —— 可在 Inspector 中独立引用。
    /// 通过 <see cref="StripSpriteVisualCatalog"/> + <see cref="visualId"/> 切换船体样貌。
    /// </summary>
    [CreateAssetMenu(menuName = "NineGrid/Data/Enemy Ship Data", fileName = "EnemyShipData")]
    public sealed class EnemyShipDataSO : ScriptableObject
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
}
