using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内遗物栏表现单例：从 Core PlayerModel.RelicDefIds 读写，刷到 RelicPanelAnchors 子槽图标。
    /// </summary>
    public sealed class RelicManagerSingleton : MonoBehaviour
    {
        public const string DefaultAnchorsName = "RelicPanelAnchors";
        private const string DefaultCatalogAssetPath = "Assets/Arts/ContentVisual/RelicVisualCatalog.asset";


        [Tooltip("遗物栏锚点根；留空则运行时按名查找 RelicPanelAnchors。")]
        [SerializeField] private Transform panelAnchors;

        [Tooltip("遗物图标 Catalog；留空时 Editor 下自动从 Arts/ContentVisual 加载。")]
        [SerializeField] private RelicVisualCatalogSO visualCatalog;

        private SpriteRenderer[] _slotRenderers = System.Array.Empty<SpriteRenderer>();
        private readonly List<string> _displayedDefIds = new();

        public IReadOnlyList<string> DisplayedDefIds => _displayedDefIds;

        private void Awake()
        {
            EnsureBindings();
        }

        private void OnDestroy()
        {
        }

        /// <summary>
        /// 从内核 PlayerModel 同步遗物栏图标。
        /// </summary>
        public void SyncFromCore()
        {
            EnsureBindings();
            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                Clear();
                return;
            }

            ApplyDefIds(arch.GetModel<PlayerModel>().RelicDefIds);
        }

        public void ApplyDefIds(IReadOnlyList<string> defIds)
        {
            EnsureBindings();
            _displayedDefIds.Clear();
            if (defIds != null)
            {
                for (var i = 0; i < defIds.Count; i++)
                {
                    if (!string.IsNullOrEmpty(defIds[i]))
                    {
                        _displayedDefIds.Add(defIds[i]);
                    }
                }
            }

            ContentIconSlotBinder.Apply(_slotRenderers, _displayedDefIds, visualCatalog);
        }

        public void Clear()
        {
            EnsureBindings();
            _displayedDefIds.Clear();
            ContentIconSlotBinder.ClearAll(_slotRenderers);
        }

        /// <summary>
        /// 按当前遗物栏占位顺序解析发牌视觉起点（与图标槽 index 一致）。
        /// </summary>
        public bool TryGetDealOrigin(string relicDefId, out Transform anchor)
        {
            anchor = null;
            EnsureBindings();
            if (panelAnchors == null || string.IsNullOrEmpty(relicDefId))
            {
                return false;
            }

            return ContentIconSlotBinder.TryGetSlotTransformByDefId(
                panelAnchors,
                _displayedDefIds,
                relicDefId,
                out anchor);
        }

        private void EnsureBindings()
        {
            if (panelAnchors == null)
            {
                var found = GameObject.Find(DefaultAnchorsName);
                if (found != null)
                {
                    panelAnchors = found.transform;
                }
            }

            if (visualCatalog == null)
            {
                TryLoadCatalog();
            }

            if ((_slotRenderers == null || _slotRenderers.Length == 0) && panelAnchors != null)
            {
                _slotRenderers = ContentIconSlotBinder.CollectChildRenderers(panelAnchors);
            }
        }

        private void TryLoadCatalog()
        {
            if (visualCatalog != null)
            {
                return;
            }

            var catalogSet = ContentVisualSpriteCatalogBootstrapSO.TryLoadCatalogSet();
            if (catalogSet?.relics != null)
            {
                visualCatalog = catalogSet.relics;
                return;
            }

#if UNITY_EDITOR
            visualCatalog = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(DefaultCatalogAssetPath);
#endif
        }
    }
}
