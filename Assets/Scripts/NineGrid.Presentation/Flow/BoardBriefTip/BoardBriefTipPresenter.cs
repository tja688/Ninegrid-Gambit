using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public TMP_Text BodyTextOrNull => bodyText;

        public static BoardBriefTipPresenter InstanceOrNull()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            sInstance = FindFirstObjectByType<BoardBriefTipPresenter>(FindObjectsInactive.Include);
            return sInstance;
        }

        public static BoardBriefTipPresenter EnsureExists()
        {
            // 权威：场景命名面板。禁止 AfterSceneLoad 抢先建无 TMP 孤儿后一直写空。
            var panel = FindPanelRoot();
            if (panel != null)
            {
                var onPanel = panel.GetComponent<BoardBriefTipPresenter>();
                if (onPanel == null)
                {
                    onPanel = panel.AddComponent<BoardBriefTipPresenter>();
                }

                onPanel.panelRoot = panel;
                onPanel.EnsureBindings();
                AdoptInstance(onPanel);
                return onPanel;
            }

            var existing = InstanceOrNull();
            if (existing != null)
            {
                existing.EnsureBindings();
                return existing;
            }

            var go = new GameObject(nameof(BoardBriefTipPresenter));
            var presenter = go.AddComponent<BoardBriefTipPresenter>();
            sInstance = presenter;
            return presenter;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureExists();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureExists();
        }

        private void Awake()
        {
            EnsureBindings();
            if (IsAuthoritativePanel(gameObject))
            {
                AdoptInstance(this);
            }

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
            var self = EnsureExists();
            if (!ReferenceEquals(self, this))
            {
                return self.ShowHover(text);
            }

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
            var self = EnsureExists();
            if (!ReferenceEquals(self, this))
            {
                return self.ShowNotice(text);
            }

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

        /// <summary>硬清悬停 + Notice 两路文案并刷新场景面板。</summary>
        public void HardClear()
        {
            var self = EnsureExists();
            if (!ReferenceEquals(self, this))
            {
                self.HardClear();
                return;
            }

            EnsureBindings();
            mSession.HardClear();
            ApplyVisual();
        }

        public void EnsureBindings()
        {
            if (panelRoot == null || !IsAuthoritativePanel(panelRoot))
            {
                var panel = FindPanelRoot();
                if (panel != null)
                {
                    panelRoot = panel;
                }
                else if (IsAuthoritativePanel(gameObject))
                {
                    panelRoot = gameObject;
                }
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

            if (panelRoot == null)
            {
                return;
            }

            var visible = mSession.IsVisible;
            if (panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }

            // 设计：底板 + 文字；场景里底板 SpriteRenderer 常默认关着，显示时打开。
            var board = panelRoot.GetComponent<SpriteRenderer>();
            if (board != null && board.enabled != visible)
            {
                board.enabled = visible;
            }
        }

        private static void AdoptInstance(BoardBriefTipPresenter presenter)
        {
            if (presenter == null)
            {
                return;
            }

            if (sInstance != null
                && !ReferenceEquals(sInstance, presenter)
                && IsOrphanFallback(sInstance))
            {
                var orphan = sInstance.gameObject;
                sInstance = presenter;
                if (orphan != null)
                {
                    Object.Destroy(orphan);
                }

                return;
            }

            sInstance = presenter;
        }

        private static bool IsOrphanFallback(BoardBriefTipPresenter presenter)
        {
            return presenter != null
                   && presenter.gameObject != null
                   && presenter.gameObject.name == nameof(BoardBriefTipPresenter)
                   && !IsAuthoritativePanel(presenter.gameObject);
        }

        private static bool IsAuthoritativePanel(GameObject go)
        {
            return go != null && go.name == PanelObjectName;
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
