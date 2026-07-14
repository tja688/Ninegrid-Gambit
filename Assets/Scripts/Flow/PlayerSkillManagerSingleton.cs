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
    /// 局内玩家技能栏表现单例：从 Core PlayerModel.SkillDefIds 读写，刷到 PlayerSkillPanelAnchors 子槽图标。
    /// </summary>
    public sealed class PlayerSkillManagerSingleton : MonoBehaviour
    {
        public const string DefaultAnchorsName = "PlayerSkillPanelAnchors";
        private const string DefaultCatalogAssetPath = "Assets/Arts/ContentVisual/SkillVisualCatalog.asset";

        private static PlayerSkillManagerSingleton _instance;

        [Tooltip("玩家技能栏锚点根；留空则运行时按名查找 PlayerSkillPanelAnchors。")]
        [SerializeField] private Transform panelAnchors;

        [Tooltip("技能图标 Catalog；留空时 Editor 下自动从 Arts/ContentVisual 加载。")]
        [SerializeField] private SkillVisualCatalogSO visualCatalog;

        private SpriteRenderer[] _slotRenderers = System.Array.Empty<SpriteRenderer>();
        private readonly List<string> _displayedDefIds = new();

        public static PlayerSkillManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PlayerSkillManagerSingleton>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(PlayerSkillManagerSingleton));
                        _instance = go.AddComponent<PlayerSkillManagerSingleton>();
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
        /// 从内核 PlayerModel 同步技能栏图标。
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

            ApplyDefIds(arch.GetModel<PlayerModel>().SkillDefIds);
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
        /// 按当前技能栏占位顺序解析发牌视觉起点（与图标槽 index 一致）。
        /// </summary>
        public bool TryGetDealOrigin(string skillDefId, out Transform anchor)
        {
            anchor = null;
            EnsureBindings();
            if (panelAnchors == null || string.IsNullOrEmpty(skillDefId))
            {
                return false;
            }

            return ContentIconSlotBinder.TryGetSlotTransformByDefId(
                panelAnchors,
                _displayedDefIds,
                skillDefId,
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
            if (catalogSet?.skills != null)
            {
                visualCatalog = catalogSet.skills;
                return;
            }

#if UNITY_EDITOR
            visualCatalog = AssetDatabase.LoadAssetAtPath<SkillVisualCatalogSO>(DefaultCatalogAssetPath);
#endif
        }
    }
}
