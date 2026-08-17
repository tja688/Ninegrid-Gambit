using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Flow.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NineGrid.Flow.InfoNotice
{
    /// <summary>
    /// 通用拒绝/提示消息弹窗驱动：基于场景 <c>Panels/InfoPanel/InfoWindow</c>。
    /// 浮现显示短时消息提醒，停留后自动淡出消失。
    /// 单实例保障：新拒绝产生时直接刷新文案并重走流程，不产生多实例重叠。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfoNoticePresenter : MonoBehaviour
    {
        public const string PanelObjectName = "InfoPanel";
        public const string WindowObjectName = "InfoWindow";
        public const string TextObjectName = "提示 text";

        public const float DefaultFadeInDuration = 0.15f;
        public const float DefaultDisplayDuration = 1.5f;
        public const float DefaultFadeOutDuration = 0.35f;

        private static InfoNoticePresenter sInstance;

        [Tooltip("信息面板根节点（留空自动按 Panels/InfoPanel 查找）。")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("提示窗口物体（含 SpriteRenderer 底框）。")]
        [SerializeField] private GameObject windowRoot;

        [Tooltip("窗口底框 SpriteRenderer。")]
        [SerializeField] private SpriteRenderer windowSpriteRenderer;

        [Tooltip("提示文本 TextMeshPro。")]
        [SerializeField] private TMP_Text bodyText;

        [Tooltip("浮现淡入时间（秒）。")]
        [SerializeField] private float fadeInDuration = DefaultFadeInDuration;

        [Tooltip("完全显示停留时间（秒）。")]
        [SerializeField] private float displayDuration = DefaultDisplayDuration;

        [Tooltip("淡出消失时间（秒）。")]
        [SerializeField] private float fadeOutDuration = DefaultFadeOutDuration;

        private Color mOriginalSpriteColor = Color.white;
        private Color mOriginalTextColor = Color.white;
        private bool mOriginalColorsCached;
        private float mCurrentAlpha;
        private int mGeneration;
        private CancellationTokenSource mAnimationCts;

        public static InfoNoticePresenter InstanceOrNull()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            sInstance = FindFirstObjectByType<InfoNoticePresenter>(FindObjectsInactive.Include);
            return sInstance;
        }

        public static InfoNoticePresenter EnsureExists()
        {
            var panel = FindPanelRoot();
            if (panel != null)
            {
                var onPanel = panel.GetComponent<InfoNoticePresenter>();
                if (onPanel == null)
                {
                    onPanel = panel.AddComponent<InfoNoticePresenter>();
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

            var go = new GameObject(nameof(InfoNoticePresenter));
            var presenter = go.AddComponent<InfoNoticePresenter>();
            presenter.EnsureBindings();
            AdoptInstance(presenter);
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
        }

        private void OnDestroy()
        {
            mAnimationCts?.Cancel();
            mAnimationCts?.Dispose();
            mAnimationCts = null;

            if (ReferenceEquals(sInstance, this))
            {
                sInstance = null;
            }
        }

        /// <summary>
        /// 弹出短时提示（使用默认停留时间），可指定音频反馈。
        /// </summary>
        public static int Show(string text, string cueId = null)
        {
            return Show(text, DefaultDisplayDuration, cueId);
        }

        /// <summary>
        /// 弹出短时提示（自定义停留时间），可指定音频反馈。
        /// </summary>
        public static int Show(string text, float durationSeconds, string cueId = null)
        {
            var presenter = EnsureExists();
            return presenter.ShowNotice(text, durationSeconds, cueId);
        }

        /// <summary>
        /// 立即隐藏并重置状态。
        /// </summary>
        public static void Hide()
        {
            var presenter = InstanceOrNull();
            presenter?.HideNotice();
        }

        public static void ResetForTests()
        {
            if (sInstance != null)
            {
                sInstance.mAnimationCts?.Cancel();
                sInstance.mAnimationCts?.Dispose();
                sInstance.mAnimationCts = null;
                sInstance = null;
            }
        }

        public bool IsVisible => windowRoot != null && windowRoot.activeSelf && mCurrentAlpha > 0.001f;

        public int ShowNotice(string text, string cueId = null)
        {
            return ShowNotice(text, displayDuration, cueId);
        }

        public int ShowNotice(string text, float durationSeconds, string cueId = null)
        {
            EnsureBindings();

            if (!string.IsNullOrEmpty(cueId))
            {
                TriggerPulseHub.PulseAudio(new NineGrid.Content.Audio.AudioCueRequest(
                    cueId,
                    "InfoNoticePresenter.ShowNotice",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
            }

            if (string.IsNullOrEmpty(text))
            {
                HideNotice();
                return 0;
            }

            // 单实例机制：取消当前进行中的淡出/停留流程
            mAnimationCts?.Cancel();
            mAnimationCts?.Dispose();
            mAnimationCts = new CancellationTokenSource();

            var gen = ++mGeneration;

            if (panelRoot != null && !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            if (windowRoot != null && !windowRoot.activeSelf)
            {
                windowRoot.SetActive(true);
            }

            if (bodyText != null)
            {
                bodyText.text = text;
            }

            // 立即刷新为完全显示状态并走淡出流程
            ApplyAlpha(1f);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                mAnimationCts.Token,
                this != null ? destroyCancellationToken : CancellationToken.None);

            RunNoticeLifecycleAsync(gen, durationSeconds, linkedCts.Token).Forget();
            return gen;
        }

        public void HideNotice()
        {
            mAnimationCts?.Cancel();
            mAnimationCts?.Dispose();
            mAnimationCts = null;
            mGeneration++;

            ApplyAlpha(0f);
            if (windowRoot != null)
            {
                windowRoot.SetActive(false);
            }
        }

        public void EnsureBindings()
        {
            if (panelRoot == null)
            {
                panelRoot = FindPanelRoot();
            }

            if (windowRoot == null && panelRoot != null)
            {
                var win = panelRoot.transform.Find(WindowObjectName);
                if (win != null)
                {
                    windowRoot = win.gameObject;
                }
            }

            if (windowRoot == null)
            {
                var win = FindWindowInSceneAll();
                if (win != null)
                {
                    windowRoot = win.gameObject;
                }
            }

            if (windowSpriteRenderer == null && windowRoot != null)
            {
                windowSpriteRenderer = windowRoot.GetComponent<SpriteRenderer>();
            }

            if (bodyText == null && windowRoot != null)
            {
                bodyText = windowRoot.GetComponentInChildren<TMP_Text>(true);
            }

            if (!mOriginalColorsCached)
            {
                if (windowSpriteRenderer != null)
                {
                    mOriginalSpriteColor = windowSpriteRenderer.color;
                }

                if (bodyText != null)
                {
                    mOriginalTextColor = bodyText.color;
                }

                mOriginalColorsCached = true;
            }
        }

        private async UniTaskVoid RunNoticeLifecycleAsync(int gen, float durationSeconds, CancellationToken ct)
        {
            try
            {
                // 1. 停留展示
                if (durationSeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(durationSeconds),
                        ignoreTimeScale: true,
                        cancellationToken: ct);
                }

                // 2. 平滑淡出
                if (fadeOutDuration > 0.001f)
                {
                    var elapsed = 0f;
                    while (elapsed < fadeOutDuration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        var t = Mathf.Clamp01(elapsed / fadeOutDuration);
                        ApplyAlpha(Mathf.Lerp(1f, 0f, t));
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }

                ApplyAlpha(0f);

                if (windowRoot != null && gen == mGeneration)
                {
                    windowRoot.SetActive(false);
                }
            }
            catch (OperationCanceledException)
            {
                // 被新拒绝打断或对象销毁，属正常控制流
            }
        }

        private void ApplyAlpha(float alpha)
        {
            mCurrentAlpha = Mathf.Clamp01(alpha);

            if (windowSpriteRenderer != null)
            {
                var c = mOriginalSpriteColor;
                c.a *= mCurrentAlpha;
                windowSpriteRenderer.color = c;
            }

            if (bodyText != null)
            {
                var c = mOriginalTextColor;
                c.a *= mCurrentAlpha;
                bodyText.color = c;
                bodyText.SetVerticesDirty();
            }
        }

        private static void AdoptInstance(InfoNoticePresenter presenter)
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
                    Destroy(orphan);
                }

                return;
            }

            sInstance = presenter;
        }

        private static bool IsOrphanFallback(InfoNoticePresenter presenter)
        {
            return presenter != null
                   && presenter.gameObject != null
                   && presenter.gameObject.name == nameof(InfoNoticePresenter)
                   && !IsAuthoritativePanel(presenter.gameObject);
        }

        private static bool IsAuthoritativePanel(GameObject go)
        {
            return go != null && (go.name == PanelObjectName || go.name == WindowObjectName);
        }

        private static GameObject FindPanelRoot()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != PanelObjectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t.gameObject;
            }

            return GameObject.Find(PanelObjectName);
        }

        private static Transform FindWindowInSceneAll()
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != WindowObjectName || !t.gameObject.scene.IsValid())
                {
                    continue;
                }

                return t;
            }

            return null;
        }
    }
}
