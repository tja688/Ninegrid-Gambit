using TMPro;
using UnityEngine;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 场景 <c>Panels/简要解释文字框</c> 驱动：悬停一句话 + 胜负 Notice。
    /// 不接已退役的 DescriptionManager / DescriptionDisplayHook。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardBriefTipPresenter : MonoBehaviour
    {
        public const string PanelObjectName = "简要解释文字框";

        private static BoardBriefTipPresenter sInstance;

        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text bodyText;

        private readonly BoardBriefTipSession mSession = new BoardBriefTipSession();

        public BoardBriefTipSession Session => mSession;

        public static BoardBriefTipPresenter InstanceOrNull()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            sInstance = FindFirstObjectByType<BoardBriefTipPresenter>();
            return sInstance;
        }

        public static BoardBriefTipPresenter EnsureExists()
        {
            var existing = InstanceOrNull();
            if (existing != null)
            {
                existing.EnsureBindings();
                return existing;
            }

            var panel = FindPanelRoot();
            if (panel == null)
            {
                var go = new GameObject(nameof(BoardBriefTipPresenter));
                var presenter = go.AddComponent<BoardBriefTipPresenter>();
                sInstance = presenter;
                return presenter;
            }

            var onPanel = panel.GetComponent<BoardBriefTipPresenter>();
            if (onPanel == null)
            {
                onPanel = panel.AddComponent<BoardBriefTipPresenter>();
            }

            onPanel.panelRoot = panel;
            onPanel.EnsureBindings();
            sInstance = onPanel;
            return onPanel;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
        }

        private void Awake()
        {
            sInstance = this;
            EnsureBindings();
            ApplyVisual();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }
        }

        public int ShowHover(string text)
        {
            EnsureBindings();
            var gen = mSession.ShowHover(text);
            ApplyVisual();
            return gen;
        }

        public void ClearHover(int generation)
        {
            mSession.ClearHover(generation);
            ApplyVisual();
        }

        public void ClearHover()
        {
            mSession.ClearHover();
            ApplyVisual();
        }

        public int ShowNotice(string text)
        {
            EnsureBindings();
            var gen = mSession.ShowNotice(text);
            ApplyVisual();
            return gen;
        }

        public void ClearNotice(int generation)
        {
            mSession.ClearNotice(generation);
            ApplyVisual();
        }

        public void ClearNotice()
        {
            mSession.ClearNotice();
            ApplyVisual();
        }

        public void EnsureBindings()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject.name == PanelObjectName
                    ? gameObject
                    : FindPanelRoot();
            }

            if (bodyText == null && panelRoot != null)
            {
                bodyText = panelRoot.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void ApplyVisual()
        {
            var text = mSession.DisplayText;
            if (bodyText != null)
            {
                bodyText.text = text;
            }

            if (panelRoot != null)
            {
                var visible = mSession.IsVisible;
                if (panelRoot.activeSelf != visible)
                {
                    panelRoot.SetActive(visible);
                }
            }
        }

        private static GameObject FindPanelRoot()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != PanelObjectName)
                {
                    continue;
                }

                if (!t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return GameObject.Find(PanelObjectName);
        }
    }
}
