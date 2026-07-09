using NineGrid.Cards;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内描述管理单例：合法 hover/drag 对象时，把 ContentVisual 描述写入 Card Info Text。
    /// </summary>
    public sealed class DescriptionManagerSingleton : MonoBehaviour
    {
        public const int MaxDescriptionChars = 45;
        private const string DefaultInfoRootName = "InGameInfoText";
        private const string DefaultCardInfoTextName = "Card Info Text";

        private static DescriptionManagerSingleton _instance;

        [Tooltip("局内描述 TMP；留空则运行时在 InGameInfoText 下按名查找 Card Info Text。")]
        [SerializeField] private TextMeshProUGUI cardInfoText;

        private int _generation;
        private DescriptionShowRoute _activeRoute = DescriptionShowRoute.Hover;
        private string _activeDefId = string.Empty;
        private ContentVisualCatalog _visualCatalog;
        private CardFrameStyleCatalog _frameStyleCatalog;
        private bool _visualsResolved;

        public static DescriptionManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<DescriptionManagerSingleton>();
                    if (_instance == null)
                    {
                        var go = new GameObject(nameof(DescriptionManagerSingleton));
                        _instance = go.AddComponent<DescriptionManagerSingleton>();
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 仅查找，不创建。销毁期调用避免残留对象。
        /// </summary>
        public static DescriptionManagerSingleton TryGetInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindFirstObjectByType<DescriptionManagerSingleton>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureBindings();
            RegisterHoverSink();
            Clear();
        }

        private void OnDestroy()
        {
            UnregisterHoverSink();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 显示 defId 对应描述；返回 generation，exit 时带同值 Clear 可防竞态。
        /// Drag 路由目前默认走与 Hover 相同的 ContentVisual 描述，后续可在此分支专属文案。
        /// Drag 优先于 Hover：拖拽中忽略 Hover 的 Show，避免被其它槽位悬停盖掉。
        /// </summary>
        public int Show(string defId, DescriptionShowRoute route = DescriptionShowRoute.Hover)
        {
            EnsureBindings();

            if (cardInfoText == null)
            {
                _generation++;
                return _generation;
            }

            // 拖拽描述占用中时，Hover 不得抢占；返回 -1 避免调用方 Clear(token) 误清 Drag
            if (route == DescriptionShowRoute.Hover
                && !string.IsNullOrEmpty(_activeDefId)
                && _activeRoute == DescriptionShowRoute.Drag)
            {
                return -1;
            }

            if (string.IsNullOrEmpty(defId))
            {
                return ClearActiveAndBump();
            }

            // 同 defId + 同路由已在展示时不 bump generation，避免每帧重申把外部 Clear(token) 弄失效
            if (_activeDefId == defId
                && _activeRoute == route
                && !string.IsNullOrEmpty(cardInfoText.text))
            {
                return _generation;
            }

            if (!TryResolveDescription(defId, route, out var description))
            {
                return ClearActiveAndBump();
            }

            _generation++;
            _activeDefId = defId;
            _activeRoute = route;
            cardInfoText.text = ClampDescription(description, MaxDescriptionChars);
            return _generation;
        }

        /// <summary>兼容旧调用：按 defId 走 Hover 路由。</summary>
        public int Show(string defId) => Show(defId, DescriptionShowRoute.Hover);

        public void Clear()
        {
            Clear(_generation);
        }

        public void Clear(int generation)
        {
            if (generation != _generation)
            {
                return;
            }

            EnsureBindings();
            _activeDefId = string.Empty;
            _activeRoute = DescriptionShowRoute.Hover;
            if (cardInfoText != null)
            {
                cardInfoText.text = string.Empty;
            }
        }

        /// <summary>仅当当前展示路由匹配时清空，避免 hover/drag 互相踩。</summary>
        public void ClearRoute(DescriptionShowRoute route)
        {
            if (string.IsNullOrEmpty(_activeDefId) || _activeRoute != route)
            {
                return;
            }

            Clear(_generation);
        }

        private int ClearActiveAndBump()
        {
            _generation++;
            _activeDefId = string.Empty;
            if (cardInfoText != null)
            {
                cardInfoText.text = string.Empty;
            }

            return _generation;
        }

        private void RegisterHoverSink()
        {
            DescriptionHoverSink.Show = ShowFromSink;
            DescriptionHoverSink.Clear = ClearFromSink;
        }

        private void UnregisterHoverSink()
        {
            if (DescriptionHoverSink.Show == ShowFromSink)
            {
                DescriptionHoverSink.Show = null;
            }

            if (DescriptionHoverSink.Clear == ClearFromSink)
            {
                DescriptionHoverSink.Clear = null;
            }
        }

        private void ShowFromSink(string defId, DescriptionShowRoute route) => Show(defId, route);

        private void ClearFromSink(DescriptionShowRoute route) => ClearRoute(route);

        private void EnsureBindings()
        {
            if (cardInfoText != null)
            {
                return;
            }

            var root = GameObject.Find(DefaultInfoRootName);
            if (root == null)
            {
                var overlay = GameObject.Find("TableNine Text Overlay UI");
                if (overlay != null)
                {
                    var t = overlay.transform.Find(DefaultInfoRootName);
                    if (t != null)
                    {
                        root = t.gameObject;
                    }
                }
            }

            if (root == null)
            {
                return;
            }

            var child = root.transform.Find(DefaultCardInfoTextName);
            if (child != null)
            {
                cardInfoText = child.GetComponent<TextMeshProUGUI>();
            }

            if (cardInfoText == null)
            {
                cardInfoText = root.GetComponentInChildren<TextMeshProUGUI>(true);
                if (cardInfoText != null && cardInfoText.gameObject.name != DefaultCardInfoTextName)
                {
                    // 避免误绑到 PlayerInfoText 子节点；仅接受具名 Card Info Text
                    cardInfoText = null;
                }
            }
        }

        private bool TryResolveDescription(
            string defId,
            DescriptionShowRoute route,
            out string description)
        {
            description = string.Empty;

            // Drag 专属描述路由预留：当前与 Hover 相同，后续可在此分支替换文案来源。
            _ = route;

            EnsureVisualsLoaded();
            CoreCardPresentationMapper.EnsureContentCatalogLoaded();

            var arch = NineGridArchitecture.Current;
            if (arch == null || _visualCatalog == null)
            {
                return false;
            }

            var content = arch.GetSystem<IContentSystem>();
            if (!content.HasCatalog)
            {
                return false;
            }

            if (!ContentVisualResolver.TryResolve(
                    defId,
                    content.Catalog,
                    _visualCatalog,
                    _frameStyleCatalog,
                    spriteProvider: null,
                    out var resolved))
            {
                return false;
            }

            description = resolved.Description ?? string.Empty;
            return !string.IsNullOrWhiteSpace(description);
        }

        private void EnsureVisualsLoaded()
        {
            if (_visualsResolved)
            {
                return;
            }

            _visualsResolved = true;
            ContentVisualBootstrap.TryLoad(
                ContentVisualBootstrap.ResolveLubanDataDirectory(),
                out _visualCatalog,
                out _frameStyleCatalog);
        }

        /// <summary>
        /// 限 MaxChars；换行符（\n / \r）计入长度。
        /// </summary>
        internal static string ClampDescription(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || maxChars <= 0)
            {
                return string.Empty;
            }

            if (text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(0, maxChars);
        }
    }
}
