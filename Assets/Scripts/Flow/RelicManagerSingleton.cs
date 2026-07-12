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

        private static RelicManagerSingleton _instance;

        [Tooltip("遗物栏锚点根；留空则运行时按名查找 RelicPanelAnchors。")]
        [SerializeField] private Transform panelAnchors;

        [Tooltip("遗物图标 Catalog；留空时 Editor 下自动从 Arts/ContentVisual 加载。")]
        [SerializeField] private RelicVisualCatalogSO visualCatalog;

        private SpriteRenderer[] _slotRenderers = System.Array.Empty<SpriteRenderer>();
        private readonly List<string> _displayedDefIds = new();

        public static RelicManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<RelicManagerSingleton>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(RelicManagerSingleton));
                        _instance = go.AddComponent<RelicManagerSingleton>();
                    }
                }

                return _instance;
            }
        }

        public IReadOnlyList<string> DisplayedDefIds => _displayedDefIds;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureBindings();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
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
#if UNITY_EDITOR
            visualCatalog = AssetDatabase.LoadAssetAtPath<RelicVisualCatalogSO>(DefaultCatalogAssetPath);
#endif
        }
    }
}
