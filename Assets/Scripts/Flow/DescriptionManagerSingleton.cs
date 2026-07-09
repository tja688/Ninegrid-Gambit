using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内描述管理单例：合法 hover 对象时，把 ContentVisual 描述写入 Card Info Text。
    /// </summary>
    public sealed class DescriptionManagerSingleton : MonoBehaviour
    {
        public const int MaxDescriptionChars = 45;
        private const string DefaultInfoRootName = "InGameInfo Text";
        private const string DefaultCardInfoTextName = "Card Info Text";

        private static DescriptionManagerSingleton _instance;

        [Tooltip("局内描述 TMP；留空则运行时在 InGameInfo Text 下按名查找 Card Info Text。")]
        [SerializeField] private TextMeshProUGUI cardInfoText;

        private int _generation;
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
        /// </summary>
        public int Show(string defId)
        {
            EnsureBindings();
            _generation++;
            var gen = _generation;

            if (cardInfoText == null)
            {
                return gen;
            }

            if (string.IsNullOrEmpty(defId))
            {
                cardInfoText.text = string.Empty;
                return gen;
            }

            if (!TryResolveDescription(defId, out var description))
            {
                cardInfoText.text = string.Empty;
                return gen;
            }

            cardInfoText.text = ClampDescription(description, MaxDescriptionChars);
            return gen;
        }

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
            if (cardInfoText != null)
            {
                cardInfoText.text = string.Empty;
            }
        }

        private void RegisterHoverSink()
        {
            Cards.DescriptionHoverSink.Show = ShowFromSink;
            Cards.DescriptionHoverSink.Clear = ClearFromSink;
        }

        private void UnregisterHoverSink()
        {
            if (Cards.DescriptionHoverSink.Show == ShowFromSink)
            {
                Cards.DescriptionHoverSink.Show = null;
            }

            if (Cards.DescriptionHoverSink.Clear == ClearFromSink)
            {
                Cards.DescriptionHoverSink.Clear = null;
            }
        }

        private void ShowFromSink(string defId) => Show(defId);

        private void ClearFromSink() => Clear();

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

        private bool TryResolveDescription(string defId, out string description)
        {
            description = string.Empty;
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
