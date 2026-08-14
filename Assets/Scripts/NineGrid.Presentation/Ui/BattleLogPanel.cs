using System.Text;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.BattleLog;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 战斗日志面板场景接线：「战斗日志按钮」开关，面板内 Scroll View 用一段 TMP 富文本呈现
    /// <see cref="BattleLogStore"/>。
    ///
    /// 层级定位是**场地层中间态**：排序层压在半黑屏之下（打开任何正式 UI 都会盖住它），
    /// 命中上也在覆层激活时让位——所以它既不阻塞别的 UI，也不需要独占半黑屏。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class BattleLogPanel : MonoBehaviour
    {
        public const string PanelRootName = "战斗日志BG";
        public const string ToggleButtonName = "战斗日志按钮";
        public const string CloseButtonName = "关闭面板 (1)";
        public const string ContentPath = "canvas/Scroll View/Viewport/Content";
        public const string ViewportPath = "canvas/Scroll View/Viewport";

        /// <summary>命中排序（均为 Overlay 表面）：占用 6~8 段，避开既有 0~4 与作弊面板 10000+。</summary>
        private const int ToggleHitSort = 6;
        private const int SwallowHitSort = 7;
        private const int CloseHitSort = 8;

        private const string TextObjectName = "__BattleLogText";
        private const float SidePadding = 10f;
        private const int MaxRenderedLines = 400;
        private const int MaxRenderedChars = 20000;

        private static BattleLogPanel sInstance;

        [SerializeField] private TMP_FontAsset logFont;
        [SerializeField] private float fontSize = 32f;

        private readonly StringBuilder mBuilder = new StringBuilder(4096);
        private GameObject mPanelRoot;
        private GameObject mToggleButton;
        private BoxCollider2D mToggleCollider;
        private BoxCollider2D mSwallowCollider;
        private BoxCollider2D mCloseCollider;
        private RectTransform mContent;
        private RectTransform mViewport;
        private TextMeshProUGUI mText;
        private bool mBound;
        private bool mDirty;
        private bool mSubscribed;

        public static bool IsOpen =>
            sInstance != null && sInstance.mPanelRoot != null && sInstance.mPanelRoot.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 面板默认失活，场景组件不会自行 Awake：这里强制接线，否则按钮永远挂不上命中代理。
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

        public static void Toggle()
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            live?.SetOpen(!IsOpen);
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

            mDirty = true;
        }

        private void OnDestroy()
        {
            Unsubscribe();
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

            // 覆层（半黑屏 / 功能菜单 / 详述）激活时让出命中：视觉被盖住，交互也不抢。
            var yielded = BattleUiDimmerOverlay.IsActive;
            SetColliderEnabled(mSwallowCollider, !yielded);
            SetColliderEnabled(mCloseCollider, !yielded);

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape) && !yielded)
            {
                SetOpen(false);
                return;
            }

            TryHandleClosePointer();

            if (mDirty)
            {
                mDirty = false;
                Rebuild();
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

            mToggleButton = FindSceneNamed(ToggleButtonName);
            WireToggleButton(mToggleButton);
            WirePanelSwallow(mPanelRoot);
            WireCloseButton(FindChildRecursive(mPanelRoot.transform, CloseButtonName));
            EnsureTextView();

            mBound = true;
            Subscribe();
        }

        private void Subscribe()
        {
            if (mSubscribed)
            {
                return;
            }

            BattleLogStore.Changed += OnStoreChanged;
            mSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!mSubscribed)
            {
                return;
            }

            BattleLogStore.Changed -= OnStoreChanged;
            mSubscribed = false;
        }

        private void OnStoreChanged()
        {
            // 面板关着时只打脏标记，下次打开一次性重建。
            mDirty = true;
        }

        private void SetOpen(bool open)
        {
            EnsureInstanceState();
            EnsureBound();
            if (mPanelRoot == null || open == mPanelRoot.activeSelf)
            {
                SyncToggleCollider();
                return;
            }

            mPanelRoot.SetActive(open);
            if (open)
            {
                Rebuild();
                mDirty = false;
                ScrollToBottom();
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiConfirm,
                    "BattleLogPanel.SetOpen",
                    "battle_log.open");
            }
            else
            {
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiCancel,
                    "BattleLogPanel.SetOpen",
                    "battle_log.close");
            }

            SyncToggleCollider();
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

        // ==================== 文本呈现 ====================

        private void EnsureTextView()
        {
            var content = mPanelRoot.transform.Find(ContentPath) as RectTransform;
            if (content == null)
            {
                Debug.LogWarning("[BattleLogPanel] 未找到 " + ContentPath + "，日志无法呈现。");
                return;
            }

            mContent = content;
            mViewport = mPanelRoot.transform.Find(ViewportPath) as RectTransform;

            var existing = content.Find(TextObjectName) as RectTransform;
            if (existing == null)
            {
                var go = new GameObject(TextObjectName, typeof(RectTransform));
                existing = (RectTransform)go.transform;
                existing.SetParent(content, false);
            }

            existing.anchorMin = new Vector2(0f, 1f);
            existing.anchorMax = new Vector2(1f, 1f);
            existing.pivot = new Vector2(0.5f, 1f);
            existing.anchoredPosition = Vector2.zero;
            existing.sizeDelta = new Vector2(0f, existing.sizeDelta.y);
            existing.localScale = Vector3.one;

            mText = existing.GetComponent<TextMeshProUGUI>();
            if (mText == null)
            {
                mText = existing.gameObject.AddComponent<TextMeshProUGUI>();
            }

            var font = ResolveFont();
            if (font != null)
            {
                mText.font = font;
            }

            mText.fontSize = fontSize;
            mText.textWrappingMode = TextWrappingModes.Normal;
            mText.richText = true;
            mText.alignment = TextAlignmentOptions.TopLeft;
            mText.color = ResolveBodyColor();
            mText.raycastTarget = false;
            mText.margin = new Vector4(SidePadding, SidePadding, SidePadding, SidePadding);
        }

        private static Color ResolveBodyColor()
        {
            return ColorUtility.TryParseHtmlString(BattleLogPalette.Body, out var color)
                ? color
                : new Color(0.23f, 0.18f, 0.13f, 1f);
        }

        private TMP_FontAsset ResolveFont()
        {
            if (logFont != null)
            {
                return logFont;
            }

            // 场景里已有的 UGUI 文本即项目字体权威（SmileySans）；缺失才落 TMP 默认。
            var probes = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < probes.Length; i++)
            {
                var probe = probes[i];
                if (probe != null && probe != mText && probe.font != null)
                {
                    return probe.font;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private void Rebuild()
        {
            if (mText == null)
            {
                EnsureTextView();
                if (mText == null)
                {
                    return;
                }
            }

            mText.text = BuildRichText();
            mText.ForceMeshUpdate();

            if (mContent != null)
            {
                var height = Mathf.Max(mText.preferredHeight + SidePadding * 2f, 1f);
                mContent.sizeDelta = new Vector2(mContent.sizeDelta.x, height);
                ((RectTransform)mText.transform).sizeDelta = new Vector2(0f, height);
            }
        }

        private string BuildRichText()
        {
            mBuilder.Clear();
            var sections = BattleLogStore.Sections;
            if (sections.Count == 0)
            {
                return BattleLogPalette.Wrap(BattleLogPalette.Muted, "（本局尚无战斗记录）");
            }

            // 从最新往回收集，够量即止；渲染时再翻正，保证「打开就看到当前房间、往上翻是历史」。
            var start = sections.Count - 1;
            var lines = 0;
            var chars = 0;
            for (var i = sections.Count - 1; i >= 0; i--)
            {
                var section = sections[i];
                var cost = section.Entries.Count + 1;
                if (i < sections.Count - 1 && (lines + cost > MaxRenderedLines || chars > MaxRenderedChars))
                {
                    break;
                }

                lines += cost;
                chars += EstimateChars(section);
                start = i;
            }

            if (start > 0)
            {
                mBuilder.Append(BattleLogPalette.Wrap(BattleLogPalette.Muted, "…更早的记录已省略"));
                mBuilder.Append('\n');
            }

            for (var i = start; i < sections.Count; i++)
            {
                AppendSection(sections[i], i > start);
            }

            return mBuilder.ToString();
        }

        private static int EstimateChars(BattleLogSection section)
        {
            var total = section.Title != null ? section.Title.Length : 0;
            var entries = section.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                total += entries[i].RichText.Length;
            }

            return total;
        }

        private void AppendSection(BattleLogSection section, bool leadingGap)
        {
            if (leadingGap)
            {
                mBuilder.Append('\n');
            }

            var title = string.IsNullOrEmpty(section.Title) ? "战斗记录" : section.Title;
            mBuilder.Append("<b>")
                .Append(BattleLogPalette.Wrap(BattleLogPalette.Section, "── " + title + " ──"))
                .Append("</b>\n");

            var entries = section.Entries;
            if (entries.Count == 0)
            {
                mBuilder.Append(BattleLogPalette.Wrap(BattleLogPalette.Muted, "（暂无）")).Append('\n');
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Depth > 0)
                {
                    mBuilder.Append("<indent=1.6em>").Append(entry.RichText).Append("</indent>");
                }
                else
                {
                    mBuilder.Append(entry.RichText);
                }

                mBuilder.Append('\n');
            }
        }

        private void ScrollToBottom()
        {
            if (mContent == null)
            {
                return;
            }

            var viewportHeight = mViewport != null ? mViewport.rect.height : 0f;
            var y = Mathf.Max(0f, mContent.sizeDelta.y - viewportHeight);
            mContent.anchoredPosition = new Vector2(mContent.anchoredPosition.x, y);
        }

        // ==================== 命中接线 ====================

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
                    "BattleLogPanel.Toggle",
                    "battle_log.hover"));

            // 面板默认失活，自身 Update 不跑：按钮可点性交给挂在按钮上的常驻门禁。
            if (button.GetComponent<ToggleButtonGate>() == null)
            {
                button.AddComponent<ToggleButtonGate>();
            }

            SyncToggleCollider();
        }

        private void WireCloseButton(Transform close)
        {
            if (close == null)
            {
                Debug.LogWarning("[BattleLogPanel] 未找到 " + CloseButtonName + "，关闭按钮无法接线。");
                return;
            }

            mCloseCollider = EnsureCollider(close.gameObject, PreferSpriteSize(close));
            var hit = close.GetComponent<WorldUiHitButton>();
            if (hit == null)
            {
                hit = close.gameObject.AddComponent<WorldUiHitButton>();
            }

            hit.Configure(
                () => SetOpen(false),
                CloseHitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.12f);
        }

        /// <summary>
        /// 关闭钮与 Scroll View 同面板：指针落在 WorldSpace uGUI 上时
        /// <see cref="PointerHitRouter"/> 会让出世界命中，须在此兜底。
        /// </summary>
        private void TryHandleClosePointer()
        {
            if (mCloseCollider == null
                || !mCloseCollider.enabled
                || BattleUiDimmerOverlay.IsActive
                || !WorldPointerUtility.WasPrimaryPressedThisFrame())
            {
                return;
            }

            var planeZ = mCloseCollider.transform.position.z;
            if (!WorldPointerUtility.TryGetPointerWorldOnPlane(null, planeZ, out var world))
            {
                return;
            }

            if (mCloseCollider.OverlapPoint(world))
            {
                SetOpen(false);
            }
        }

        /// <summary>
        /// 面板铺一层吞点面：Scroll View 那块由 uGUI 自己挡住世界命中，
        /// 但边框区域没有 Graphic，不吞就会点穿到棋盘。
        /// </summary>
        private void WirePanelSwallow(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            var sr = panel.GetComponent<SpriteRenderer>();
            var size = sr != null && sr.sprite != null
                ? (Vector2)sr.sprite.bounds.size
                : new Vector2(4f, 6f);
            mSwallowCollider = EnsureCollider(panel, size);

            var hit = panel.GetComponent<WorldUiHitButton>();
            if (hit == null)
            {
                hit = panel.AddComponent<WorldUiHitButton>();
            }

            hit.Configure(null, SwallowHitSort, PointerHitSurfacePriorities.Overlay);
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

        private static BattleLogPanel FindSceneInstance()
        {
            var all = Resources.FindObjectsOfTypeAll<BattleLogPanel>();
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

        private static BattleLogPanel EnsureFromScene()
        {
            var root = FindSceneNamed(PanelRootName);
            if (root == null)
            {
                return null;
            }

            var panel = root.GetComponent<BattleLogPanel>();
            return panel != null ? panel : root.AddComponent<BattleLogPanel>();
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

        private static Transform FindChildRecursive(Transform parent, string objectName)
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

                var found = FindChildRecursive(child, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && go.scene.isLoaded;
        }

        /// <summary>
        /// 「战斗日志」按钮的可点门禁：主菜单、覆层激活、面板已开时一律不可点。
        /// 按钮常驻激活，所以这层判断挂在按钮上，而不是默认失活的面板上。
        /// </summary>
        [DisallowMultipleComponent]
        [DefaultExecutionOrder(99)]
        private sealed class ToggleButtonGate : MonoBehaviour
        {
            private BoxCollider2D mCollider;

            private void Awake()
            {
                mCollider = GetComponent<BoxCollider2D>();
            }

            private void Update()
            {
                if (mCollider == null)
                {
                    return;
                }

                var allowed = !IsOpen && !BattleUiDimmerOverlay.IsActive && IsInRun();
                if (mCollider.enabled != allowed)
                {
                    mCollider.enabled = allowed;
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
