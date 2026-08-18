using System;
using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 一开页（左页 + 右页）。Inspector 列表顺序即第 1、2、3… 开页；空槽允许。
    /// </summary>
    [Serializable]
    public class TutorialKnowledgeSpread
    {
        [Tooltip("该开页的左页内容根（可空）")]
        public GameObject leftPage;

        [Tooltip("该开页的右页内容根（可空）")]
        public GameObject rightPage;
    }

    /// <summary>
    /// 教学知识库书本：点「教程按钮」打开，半黑屏 / Esc 关闭。
    /// 翻页播 InventoryBook 帧，在纸页盖住内容的那一帧再切左右页，避免瞬切穿帮。
    /// 挂在场景 <c>UI面板/教学知识库BG</c> 上，Inspector 里「开页列表」即综合控制。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("NineGrid/教学知识库综合控制")]
    public sealed class TutorialKnowledgeBookController : MonoBehaviour
    {
        public const string PanelRootName = "教学知识库BG";
        public const string ToggleButtonName = "教程按钮";
        public const string RuleBookButtonName = "规则书";
        public const string PrevButtonName = "上一页按钮";
        public const string NextButtonName = "下一页按钮";
        public const string ProgressLabelName = "页面进度";

        private const string DimmerReason = "tutorial-knowledge-book";
        private const int ToggleHitSort = 9;
        private const int SwallowHitSort = 10;
        private const int NavHitSort = 11;
        private const float TurnFps = 12f;
        private const int CoverFrameIndex = 2;

        private static TutorialKnowledgeBookController sInstance;

        [Header("综合控制 · 开页列表（拖排序 = 页码顺序）")]
        [SerializeField] private List<TutorialKnowledgeSpread> spreads = new List<TutorialKnowledgeSpread>();

        [Header("翻页帧（0=打开 spread，2=纸页盖住内容）")]
        [SerializeField] private Sprite[] pageTurnFrames = Array.Empty<Sprite>();

        [SerializeField] private int contentSwapFrame = CoverFrameIndex;

        private GameObject mPanelRoot;
        private GameObject mToggleButton;
        private BoxCollider2D mToggleCollider;
        private SpriteRenderer mBookRenderer;
        private TextMeshPro mProgressLabel;
        private SpriteRenderer mPrevRenderer;
        private SpriteRenderer mNextRenderer;
        private BoxCollider2D mPrevCollider;
        private BoxCollider2D mNextCollider;
        private Color mPrevBaseColor = Color.white;
        private Color mNextBaseColor = Color.white;
        private bool mBound;
        private bool mDimmerHeld;
        private bool mTurning;
        private int mSpreadIndex;
        private Coroutine mTurnRoutine;

        public static bool IsOpen =>
            sInstance != null && sInstance.mPanelRoot != null && sInstance.mPanelRoot.activeSelf;

        public static int CurrentSpreadIndex => sInstance != null ? sInstance.mSpreadIndex : -1;

        public static int SpreadCount =>
            sInstance != null && sInstance.spreads != null ? sInstance.spreads.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            if (live == null)
            {
                return;
            }

            live.EnsureInstanceState();
            if (live.mPanelRoot != null && live.mPanelRoot.activeSelf)
            {
                live.mPanelRoot.SetActive(false);
            }

            if (Application.isPlaying)
            {
                live.EnsureBound();
            }
        }

        public static void RequestOpen()
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            live?.SetOpen(true);
        }

        public static void CloseIfOpen()
        {
            if (sInstance != null)
            {
                sInstance.SetOpen(false);
            }
        }

        private void Awake()
        {
            EnsureInstanceState();
            if (Application.isPlaying)
            {
                EnsureBound();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureBound();
            }
        }

        private void OnDestroy()
        {
            if (mDimmerHeld)
            {
                BattleUiDimmerOverlay.Release(DimmerReason);
                mDimmerHeld = false;
            }

            if (sInstance == this)
            {
                sInstance = null;
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape) && !mTurning)
            {
                SetOpen(false);
            }
        }

        private void EnsureInstanceState()
        {
            sInstance = this;
            if (mPanelRoot == null)
            {
                mPanelRoot = gameObject;
            }
        }

        private void EnsureBound()
        {
            if (mBound)
            {
                return;
            }

            EnsureInstanceState();
            mBookRenderer = mPanelRoot.GetComponent<SpriteRenderer>();
            mProgressLabel = FindNamedChild(mPanelRoot.transform, ProgressLabelName)
                ?.GetComponent<TextMeshPro>();

            var uiRoot = FindSceneNamed("UI面板");
            BattleUiDimmerOverlay.EnsureBound(
                uiRoot != null ? uiRoot.transform.Find("半黑屏BG")?.gameObject : null);

            mToggleButton = FindSceneNamed(RuleBookButtonName) ?? FindSceneNamed(ToggleButtonName);
            WireToggleButton(mToggleButton);
            WirePanelSwallow(mPanelRoot);
            WireNavButton(
                FindNamedChild(mPanelRoot.transform, PrevButtonName),
                forward: false);
            WireNavButton(
                FindNamedChild(mPanelRoot.transform, NextButtonName),
                forward: true);

            mBound = true;
            ShowSpread(mSpreadIndex, hideContent: mPanelRoot.activeSelf == false);
            ApplyOpenFrame();
            RefreshNavVisuals();
            RefreshProgress();
            SyncToggleCollider();
        }

        private void SetOpen(bool open)
        {
            EnsureInstanceState();
            EnsureBound();
            if (mPanelRoot == null)
            {
                return;
            }

            if (open == mPanelRoot.activeSelf && !open)
            {
                SyncToggleCollider();
                return;
            }

            if (open)
            {
                if (PlayerAudioSettingsPanel.IsOpen
                    || CharacterSelectPanel.IsOpen
                    || PureBlackScreenOverlay.IsActive
                    || CardInspectOverlayPresenter.IsOpen)
                {
                    return;
                }

                if (!BattleUiDimmerOverlay.TryAcquire(DimmerReason))
                {
                    return;
                }

                mDimmerHeld = true;
                StopTurn();
                mSpreadIndex = 0;
                mPanelRoot.SetActive(true);
                ApplyOpenFrame();
                SetProgressVisible(true);
                ShowSpread(mSpreadIndex, hideContent: false);
                RefreshNavVisuals();
                RefreshProgress();
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiConfirm,
                    "TutorialKnowledgeBook.SetOpen",
                    "tutorial_book.open");
            }
            else
            {
                StopTurn();
                if (mPanelRoot.activeSelf)
                {
                    InteractionAudioCues.Pulse(
                        InteractionAudioCues.UiCancel,
                        "TutorialKnowledgeBook.SetOpen",
                        "tutorial_book.close");
                }

                mPanelRoot.SetActive(false);
                if (mDimmerHeld)
                {
                    BattleUiDimmerOverlay.Release(DimmerReason);
                    mDimmerHeld = false;
                }
            }

            SyncToggleCollider();
        }

        private void RequestTurn(bool forward)
        {
            if (!IsOpen || mTurning)
            {
                return;
            }

            var next = mSpreadIndex + (forward ? 1 : -1);
            if (next < 0 || next >= ResolvedSpreadCount())
            {
                return;
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "TutorialKnowledgeBook.Turn",
                forward ? "tutorial_book.next" : "tutorial_book.prev");
            mTurnRoutine = StartCoroutine(TurnRoutine(forward, next));
        }

        private IEnumerator TurnRoutine(bool forward, int nextIndex)
        {
            mTurning = true;
            RefreshNavVisuals();

            var frames = ResolvedFrames();
            var wait = new WaitForSecondsRealtime(1f / Mathf.Max(1f, TurnFps));
            var swapFrame = Mathf.Clamp(contentSwapFrame, 0, Mathf.Max(0, frames.Length - 1));

            // 正向 0→1→2→3→0；反向 0→3→2→1→0。在盖住帧切换内容。
            var sequence = forward
                ? new[] { 1, 2, 3, 0 }
                : new[] { 3, 2, 1, 0 };

            var swapped = false;
            for (var i = 0; i < sequence.Length; i++)
            {
                var frame = sequence[i];
                ApplyFrame(frame);
                SetProgressVisible(frame == 0);
                if (frame != 0)
                {
                    ShowSpread(mSpreadIndex, hideContent: true);
                }

                if (!swapped && frame == swapFrame)
                {
                    mSpreadIndex = nextIndex;
                    ShowSpread(mSpreadIndex, hideContent: true);
                    swapped = true;
                }

                if (frame == 0)
                {
                    ShowSpread(mSpreadIndex, hideContent: false);
                    RefreshProgress();
                }

                yield return wait;
            }

            if (!swapped)
            {
                mSpreadIndex = nextIndex;
            }

            ApplyOpenFrame();
            SetProgressVisible(true);
            ShowSpread(mSpreadIndex, hideContent: false);
            RefreshProgress();
            mTurning = false;
            RefreshNavVisuals();
            mTurnRoutine = null;
        }

        private void StopTurn()
        {
            if (mTurnRoutine != null)
            {
                StopCoroutine(mTurnRoutine);
                mTurnRoutine = null;
            }

            mTurning = false;
            SetProgressVisible(true);
        }

        private void ShowSpread(int index, bool hideContent)
        {
            var count = ResolvedSpreadCount();
            for (var i = 0; i < count; i++)
            {
                var spread = spreads[i];
                var visible = !hideContent && i == index;
                SetPageActive(spread != null ? spread.leftPage : null, visible);
                SetPageActive(spread != null ? spread.rightPage : null, visible);
            }
        }

        private static void SetPageActive(GameObject page, bool visible)
        {
            if (page != null && page.activeSelf != visible)
            {
                page.SetActive(visible);
            }
        }

        private void ApplyOpenFrame()
        {
            ApplyFrame(0);
        }

        private void ApplyFrame(int index)
        {
            if (mBookRenderer == null)
            {
                return;
            }

            var frames = ResolvedFrames();
            if (frames.Length == 0)
            {
                return;
            }

            var clamped = Mathf.Clamp(index, 0, frames.Length - 1);
            if (frames[clamped] != null)
            {
                mBookRenderer.sprite = frames[clamped];
            }
        }

        private Sprite[] ResolvedFrames()
        {
            return pageTurnFrames != null && pageTurnFrames.Length > 0
                ? pageTurnFrames
                : Array.Empty<Sprite>();
        }

        private int ResolvedSpreadCount()
        {
            return spreads != null ? spreads.Count : 0;
        }

        private void SetProgressVisible(bool visible)
        {
            if (mProgressLabel != null && mProgressLabel.gameObject.activeSelf != visible)
            {
                mProgressLabel.gameObject.SetActive(visible);
            }
        }

        private void RefreshProgress()
        {
            if (mProgressLabel == null)
            {
                return;
            }

            var count = Mathf.Max(1, ResolvedSpreadCount());
            mProgressLabel.text = (mSpreadIndex + 1) + "/" + count;
        }

        private void RefreshNavVisuals()
        {
            var count = ResolvedSpreadCount();
            var canPrev = !mTurning && mSpreadIndex > 0 && count > 0;
            var canNext = !mTurning && mSpreadIndex < count - 1 && count > 0;
            SetNavEnabled(mPrevCollider, mPrevRenderer, mPrevBaseColor, canPrev);
            SetNavEnabled(mNextCollider, mNextRenderer, mNextBaseColor, canNext);
        }

        private static void SetNavEnabled(
            BoxCollider2D collider,
            SpriteRenderer renderer,
            Color baseColor,
            bool enabled)
        {
            if (collider != null && collider.enabled != enabled)
            {
                collider.enabled = enabled;
            }

            if (renderer != null)
            {
                var color = baseColor;
                color.a = enabled ? baseColor.a : baseColor.a * 0.35f;
                renderer.color = color;
            }
        }

        private void SyncToggleCollider()
        {
            SetColliderEnabled(mToggleCollider, !IsOpen);
        }

        private static void SetColliderEnabled(BoxCollider2D collider, bool enabled)
        {
            if (collider != null && collider.enabled != enabled)
            {
                collider.enabled = enabled;
            }
        }

        private void WireToggleButton(GameObject button)
        {
            if (button == null)
            {
                return;
            }

            mToggleCollider = EnsureCollider(button, PreferSpriteSize(button.transform));
            var hit = button.GetComponent<WorldUiHitButton>();
            if (hit == null)
            {
                hit = button.AddComponent<WorldUiHitButton>();
            }

            hit.Configure(
                () => SetOpen(true),
                ToggleHitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.12f,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "TutorialKnowledgeBook.Toggle",
                    "tutorial_book.hover"));

            if (button.GetComponent<ToggleButtonGate>() == null)
            {
                button.AddComponent<ToggleButtonGate>();
            }

            SyncToggleCollider();
        }

        private void WirePanelSwallow(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            var sr = panel.GetComponent<SpriteRenderer>();
            var size = sr != null && sr.sprite != null
                ? (Vector2)sr.sprite.bounds.size
                : new Vector2(10f, 8f);
            EnsureCollider(panel, size);

            var hit = panel.GetComponent<WorldUiHitButton>();
            if (hit == null)
            {
                hit = panel.AddComponent<WorldUiHitButton>();
            }

            hit.Configure(null, SwallowHitSort, PointerHitSurfacePriorities.Overlay);
        }

        private void WireNavButton(Transform button, bool forward)
        {
            if (button == null)
            {
                return;
            }

            var collider = EnsureCollider(button.gameObject, PreferSpriteSize(button));
            var hit = button.GetComponent<WorldUiHitButton>();
            if (hit == null)
            {
                hit = button.gameObject.AddComponent<WorldUiHitButton>();
            }

            hit.Configure(
                () => RequestTurn(forward),
                NavHitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.12f);

            var renderer = button.GetComponent<SpriteRenderer>();
            if (forward)
            {
                mNextCollider = collider;
                mNextRenderer = renderer;
                if (renderer != null)
                {
                    mNextBaseColor = renderer.color;
                }
            }
            else
            {
                mPrevCollider = collider;
                mPrevRenderer = renderer;
                if (renderer != null)
                {
                    mPrevBaseColor = renderer.color;
                }
            }
        }

        private static BoxCollider2D EnsureCollider(GameObject go, Vector2 size)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider2D>();
            }

            if (col.size.x < 0.01f || col.size.y < 0.01f)
            {
                col.size = size;
            }

            col.isTrigger = false;
            col.enabled = true;
            return col;
        }

        private static Vector2 PreferSpriteSize(Transform t)
        {
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                var size = sr.drawMode == SpriteDrawMode.Simple
                    ? (Vector2)sr.sprite.bounds.size
                    : sr.size;
                return new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
            }

            return new Vector2(0.5f, 0.5f);
        }

        private static TutorialKnowledgeBookController FindSceneInstance()
        {
            var all = Resources.FindObjectsOfTypeAll<TutorialKnowledgeBookController>();
            for (var i = 0; i < all.Length; i++)
            {
                var panel = all[i];
                if (panel != null && IsSceneObject(panel.gameObject))
                {
                    return panel;
                }
            }

            return null;
        }

        private static TutorialKnowledgeBookController EnsureFromScene()
        {
            var root = FindSceneNamed(PanelRootName);
            if (root == null)
            {
                return null;
            }

            var panel = root.GetComponent<TutorialKnowledgeBookController>();
            return panel != null ? panel : root.AddComponent<TutorialKnowledgeBookController>();
        }

        private static GameObject FindSceneNamed(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t.name != name || !IsSceneObject(t.gameObject))
                {
                    continue;
                }

                return t.gameObject;
            }

            return null;
        }

        private static Transform FindNamedChild(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && go.scene.isLoaded;
        }

        /// <summary>
        /// 「规则书」（原「教程按钮」）门禁与显隐控制：
        /// 1. 仅在战斗壳（进入对战后，IsInRun 为真）显示并可点；
        /// 2. 主菜单（Shell 处于 MainMenu 相位）一律隐藏（禁用渲染器与文字）且禁用碰撞体；
        /// 3. 覆层 / 其它全屏面板 / 知识库已开时不可点。
        /// </summary>
        [DisallowMultipleComponent]
        [DefaultExecutionOrder(99)]
        private sealed class ToggleButtonGate : MonoBehaviour
        {
            private BoxCollider2D mCollider;
            private Renderer[] mRenderers = Array.Empty<Renderer>();
            private TMPro.TMP_Text[] mTexts = Array.Empty<TMPro.TMP_Text>();

            private void Awake()
            {
                mCollider = GetComponent<BoxCollider2D>();
                mRenderers = GetComponentsInChildren<Renderer>(true);
                mTexts = GetComponentsInChildren<TMPro.TMP_Text>(true);
            }

            private void Update()
            {
                var inRun = IsInRun();
                SetVisualsActive(inRun);

                if (mCollider == null)
                {
                    return;
                }

                var allowed = inRun
                    && !IsOpen
                    && !BattleUiDimmerOverlay.IsActive
                    && !PlayerAudioSettingsPanel.IsOpen
                    && !CharacterSelectPanel.IsOpen
                    && !PureBlackScreenOverlay.IsActive;
                if (mCollider.enabled != allowed)
                {
                    mCollider.enabled = allowed;
                }
            }

            private void SetVisualsActive(bool active)
            {
                for (var i = 0; i < mRenderers.Length; i++)
                {
                    if (mRenderers[i] != null && mRenderers[i].enabled != active)
                    {
                        mRenderers[i].enabled = active;
                    }
                }

                for (var i = 0; i < mTexts.Length; i++)
                {
                    if (mTexts[i] != null && mTexts[i].enabled != active)
                    {
                        mTexts[i].enabled = active;
                    }
                }
            }

            private static bool IsInRun()
            {
                var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
                var shell = arch?.GetSystem<IGameFlowShellSystem>();
                return shell != null && shell.State.Value != GameFlowShellState.MainMenu;
            }
        }
    }
}
