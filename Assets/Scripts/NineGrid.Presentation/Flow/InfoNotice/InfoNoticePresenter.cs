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
    /// 教学文案保持模式。
    /// </summary>
    public enum InfoNoticeHoldMode
    {
        /// <summary>非教学保持（普通短时提示，自动淡出）。</summary>
        None = 0,

        /// <summary>点了才走：保持完全显示直到推进点击或显式解除。</summary>
        ClickToAdvance = 1,

        /// <summary>停两秒：保持约 2 秒后自动淡出并解除。</summary>
        HoldTwoSeconds = 2,
    }

    /// <summary>
    /// 通用拒绝/提示消息弹窗驱动：基于场景 <c>Panels/InfoPanel/InfoWindow</c>。
    /// 浮现显示短时消息提醒或教学句，切片宽度按字数自适应（宽:字 = 2:6），高度与物体缩放不变。
    /// 支持教学保持（点了才走 / 停两秒），保持期间短时拒绝提示不会替换或打断教学句。
    /// 单实例保障：新动作产生时直接刷新文案并重走流程，不产生多实例重叠。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InfoNoticePresenter : MonoBehaviour
    {
        public const string PanelObjectName = "InfoPanel";
        public const string WindowObjectName = "InfoWindow";
        public const string TextObjectName = "提示 text";

        public const float CharacterWidthRatio = 2f / 6f;
        public const float DefaultHoldTwoSecondsDuration = 2.0f;

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
        private Vector2 mOriginalSpriteSize = new Vector2(2f, 0.8f);
        private bool mOriginalColorsCached;
        private float mCurrentAlpha;
        private int mGeneration;
        private CancellationTokenSource mAnimationCts;

        private InfoNoticeHoldMode mCurrentHoldMode = InfoNoticeHoldMode.None;
        private bool mIsHoldingTutorialSentence;

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
        /// 弹出教学句（点了才走）：持续完全显示直到推进点击或显式解除。
        /// </summary>
        public static int ShowClickToAdvance(string text, string cueId = null)
        {
            var presenter = EnsureExists();
            return presenter.ShowTutorialSentence(text, InfoNoticeHoldMode.ClickToAdvance, cueId);
        }

        /// <summary>
        /// 弹出教学句（停两秒）：完全显示约两秒后自动淡出并解除。
        /// </summary>
        public static int ShowHoldTwoSeconds(string text, string cueId = null)
        {
            var presenter = EnsureExists();
            return presenter.ShowTutorialSentence(text, InfoNoticeHoldMode.HoldTwoSeconds, cueId);
        }

        /// <summary>
        /// 解除当前正在保持的教学句。
        /// </summary>
        public static void DismissHold()
        {
            var presenter = InstanceOrNull();
            presenter?.DismissTutorialHold();
        }

        /// <summary>
        /// 当前是否正在保持教学句。
        /// </summary>
        public static bool IsHoldingSentence => InstanceOrNull()?.IsHoldingTutorialSentence ?? false;

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
                sInstance.mIsHoldingTutorialSentence = false;
                sInstance.mCurrentHoldMode = InfoNoticeHoldMode.None;
                sInstance = null;
            }
        }

        public bool IsVisible => windowRoot != null && windowRoot.activeSelf && mCurrentAlpha > 0.001f;

        public bool IsHoldingTutorialSentence => mIsHoldingTutorialSentence;

        public InfoNoticeHoldMode CurrentHoldMode => mCurrentHoldMode;

        public int ShowNotice(string text, string cueId = null)
        {
            return ShowNotice(text, displayDuration, cueId);
        }

        public int ShowNotice(string text, float durationSeconds, string cueId = null)
        {
            EnsureBindings();

            // 教学句正在保持时，短时拒绝提示不会替换或打断教学句
            if (mIsHoldingTutorialSentence)
            {
                return 0;
            }

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

            ApplySlicedWidth(text);

            // 立即刷新为完全显示状态并走淡出流程
            ApplyAlpha(1f);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                mAnimationCts.Token,
                this != null ? destroyCancellationToken : CancellationToken.None);

            RunNoticeLifecycleAsync(gen, durationSeconds, linkedCts.Token).Forget();
            return gen;
        }

        /// <summary>
        /// 弹出教学句并指定保持模式。
        /// </summary>
        public int ShowTutorialSentence(string text, InfoNoticeHoldMode mode, string cueId = null)
        {
            EnsureBindings();

            if (!string.IsNullOrEmpty(cueId))
            {
                TriggerPulseHub.PulseAudio(new NineGrid.Content.Audio.AudioCueRequest(
                    cueId,
                    "InfoNoticePresenter.ShowTutorialSentence",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
            }

            if (string.IsNullOrEmpty(text))
            {
                DismissTutorialHold();
                return 0;
            }

            mAnimationCts?.Cancel();
            mAnimationCts?.Dispose();
            mAnimationCts = new CancellationTokenSource();

            var gen = ++mGeneration;
            mIsHoldingTutorialSentence = true;
            mCurrentHoldMode = mode;

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

            ApplySlicedWidth(text);
            ApplyAlpha(1f);

            if (mode == InfoNoticeHoldMode.HoldTwoSeconds)
            {
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    mAnimationCts.Token,
                    this != null ? destroyCancellationToken : CancellationToken.None);

                RunTutorialHoldTwoSecondsLifecycleAsync(gen, DefaultHoldTwoSecondsDuration, linkedCts.Token).Forget();
            }

            return gen;
        }

        /// <summary>
        /// 解除教学保持并隐藏弹窗。
        /// </summary>
        public void DismissTutorialHold()
        {
            if (!mIsHoldingTutorialSentence)
            {
                return;
            }

            mIsHoldingTutorialSentence = false;
            mCurrentHoldMode = InfoNoticeHoldMode.None;
            HideNotice();
        }

        public void HideNotice()
        {
            mAnimationCts?.Cancel();
            mAnimationCts?.Dispose();
            mAnimationCts = null;
            mGeneration++;
            mIsHoldingTutorialSentence = false;
            mCurrentHoldMode = InfoNoticeHoldMode.None;

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

            if (windowSpriteRenderer != null)
            {
                windowSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
                if (!mOriginalColorsCached)
                {
                    mOriginalSpriteColor = windowSpriteRenderer.color;
                    mOriginalSpriteSize = windowSpriteRenderer.size;
                    if (mOriginalSpriteSize.y <= 0.001f)
                    {
                        mOriginalSpriteSize.y = 0.8f;
                    }
                }
            }

            if (!mOriginalColorsCached)
            {
                if (bodyText != null)
                {
                    mOriginalTextColor = bodyText.color;
                }

                mOriginalColorsCached = true;
            }
        }

        private void ApplySlicedWidth(string text)
        {
            if (windowSpriteRenderer == null)
            {
                return;
            }

            var count = string.IsNullOrEmpty(text) ? 0 : text.Length;
            var targetWidth = count * CharacterWidthRatio;
            windowSpriteRenderer.drawMode = SpriteDrawMode.Sliced;
            windowSpriteRenderer.size = new Vector2(targetWidth, mOriginalSpriteSize.y);
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
                // 被新动作打断或对象销毁，属正常控制流
            }
        }

        private async UniTaskVoid RunTutorialHoldTwoSecondsLifecycleAsync(int gen, float durationSeconds, CancellationToken ct)
        {
            try
            {
                // 1. 停两秒
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

                if (gen == mGeneration)
                {
                    mIsHoldingTutorialSentence = false;
                    mCurrentHoldMode = InfoNoticeHoldMode.None;
                    if (windowRoot != null)
                    {
                        windowRoot.SetActive(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 被新动作打断或显式解除，属正常控制流
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

            return GameObject.Find(WindowObjectName)?.transform;
        }
    }
}


