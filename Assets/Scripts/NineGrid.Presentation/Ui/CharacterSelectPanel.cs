using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 人物选择面板：主菜单「开始游戏」后弹出，选定角色再走正式开局。
    /// 场景预置 <c>UI面板/人物选择BG</c>（默认失活）；当前仅战士可选，另两席为未解锁黑影。
    /// 确认出发 → <see cref="GameFlowController.BeginFormalRun"/>；流程其余部分零改动。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CharacterSelectPanel : MonoBehaviour
    {
        public const string PanelRootName = "人物选择BG";
        public const string WarriorSlotName = "角色槽_战士";
        public const string LockedSlotAName = "角色槽_未解锁A";
        public const string LockedSlotBName = "角色槽_未解锁B";
        public const string ConfirmButtonName = "出发按钮";
        public const string BackButtonName = "返回按钮";
        public const string HintTextName = "提示文字";
        public const string SelectFrameName = "选中框";
        public const string StatsTextName = "属性文字";

        private const string DimmerReason = "character-select";
        private const string WarriorDefId = "avatar.default";
        private const float HintSeconds = 1.6f;

        private static CharacterSelectPanel sInstance;

        private GameObject mPanelRoot;
        private Transform mWarriorSlot;
        private Transform mLockedSlotA;
        private Transform mLockedSlotB;
        private TMP_Text mHintText;
        private bool mBound;
        private bool mDimmerHeld;
        private float mHintClearAt = -1f;

        public static bool IsOpen =>
            sInstance != null
            && sInstance.mPanelRoot != null
            && sInstance.mPanelRoot.activeSelf;

        /// <summary>打开面板；场景缺预置时返回 false（调用方回退直接开局）。</summary>
        public static bool RequestOpen()
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            if (live == null)
            {
                return false;
            }

            live.SetOpen(true);
            return true;
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

            // 面板只服务主菜单相位；QuickTest 等旁路开局时自动收起。
            var shell = ResolveShell();
            if (shell != null && shell.State.Value != GameFlowShellState.MainMenu)
            {
                SetOpen(false);
                return;
            }

            if (mHintClearAt > 0f && Time.unscaledTime >= mHintClearAt)
            {
                mHintClearAt = -1f;
                if (mHintText != null)
                {
                    mHintText.text = string.Empty;
                }
            }

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiCancel,
                    "CharacterSelectPanel.Update.Escape",
                    "character_select.close");
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

            var uiRoot = FindSceneNamed("UI面板");
            BattleUiDimmerOverlay.EnsureBound(
                uiRoot != null ? uiRoot.transform.Find("半黑屏BG")?.gameObject : null);

            WirePanelSwallow(mPanelRoot);

            mWarriorSlot = mPanelRoot.transform.Find(WarriorSlotName);
            mLockedSlotA = mPanelRoot.transform.Find(LockedSlotAName);
            mLockedSlotB = mPanelRoot.transform.Find(LockedSlotBName);
            mHintText = FindTmp(mPanelRoot.transform, HintTextName);

            WireSlot(mWarriorSlot, unlocked: true);
            WireSlot(mLockedSlotA, unlocked: false);
            WireSlot(mLockedSlotB, unlocked: false);
            WireButton(mPanelRoot.transform.Find(ConfirmButtonName), Confirm);
            WireButton(mPanelRoot.transform.Find(BackButtonName), Back);

            RefreshWarriorStats();
            RefreshSelection();
            mBound = true;
        }

        private void SetOpen(bool open)
        {
            EnsureBound();
            if (mPanelRoot == null || open == mPanelRoot.activeSelf)
            {
                return;
            }

            if (open)
            {
                if (BattleUiDimmerOverlay.TryAcquire(DimmerReason))
                {
                    mDimmerHeld = true;
                }

                mPanelRoot.SetActive(true);
                RefreshWarriorStats();
                RefreshSelection();
                ClearHint();
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiConfirm,
                    "CharacterSelectPanel.SetOpen",
                    "character_select.open");
            }
            else
            {
                mPanelRoot.SetActive(false);
                ClearHint();
                if (mDimmerHeld)
                {
                    BattleUiDimmerOverlay.Release(DimmerReason);
                    mDimmerHeld = false;
                }
            }
        }

        private void Confirm()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "CharacterSelectPanel.Confirm",
                "character_select.confirm");
            SetOpen(false);

            var controller = Object.FindFirstObjectByType<GameFlowController>();
            if (controller != null)
            {
                controller.BeginFormalRun();
                return;
            }

            Debug.LogWarning("[CharacterSelect] 未找到 GameFlowController，无法开局。");
        }

        private void Back()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiCancel,
                "CharacterSelectPanel.Back",
                "character_select.back");
            SetOpen(false);
        }

        private void OnSlotClicked(bool unlocked)
        {
            if (unlocked)
            {
                // 当前仅战士一席可选；点选即保持选中态（预留多角色扩展）。
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiPress,
                    "CharacterSelectPanel.OnSlotClicked",
                    "character_select.warrior");
                RefreshSelection();
                return;
            }

            InteractionAudioCues.Pulse(
                InteractionAudioCues.MainMenuReject,
                "CharacterSelectPanel.OnSlotClicked",
                "character_select.locked");
            ShowHint(NineGrid.Core.Localization.L10n.Tr("charselect.locked", "该角色尚未解锁"));
        }

        private void RefreshSelection()
        {
            SetSelectFrameVisible(mWarriorSlot, true);
            SetSelectFrameVisible(mLockedSlotA, false);
            SetSelectFrameVisible(mLockedSlotB, false);
        }

        private void RefreshWarriorStats()
        {
            if (mWarriorSlot == null)
            {
                return;
            }

            var statsText = FindTmp(mWarriorSlot, StatsTextName);
            if (statsText == null)
            {
                return;
            }

            var profession = ProfessionCatalog.Default;
            statsText.text = profession != null
                ? string.Format(
                    NineGrid.Core.Localization.L10n.Tr("charselect.stats", "生命 {0} · 攻击 {1}"),
                    profession.MaxHp,
                    profession.Attack)
                : string.Empty;

            var nameText = FindTmp(mWarriorSlot, "名字文字");
            if (nameText != null
                && CardPresentationConfigCatalog.TryGet(WarriorDefId, out var dto)
                && dto != null
                && !string.IsNullOrWhiteSpace(dto.displayName))
            {
                nameText.text = dto.displayName;
            }
        }

        private void ShowHint(string message)
        {
            if (mHintText == null)
            {
                return;
            }

            mHintText.text = message ?? string.Empty;
            mHintClearAt = Time.unscaledTime + HintSeconds;
        }

        private void ClearHint()
        {
            mHintClearAt = -1f;
            if (mHintText != null)
            {
                mHintText.text = string.Empty;
            }
        }

        private void WireSlot(Transform slot, bool unlocked)
        {
            if (slot == null)
            {
                return;
            }

            EnsureCollider(slot.gameObject, PreferSpriteSize(slot));
            var button = slot.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = slot.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                () => OnSlotClicked(unlocked),
                BattleUiDimmerOverlay.CloseHitSort,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: unlocked ? 1.03f : 1f);
        }

        private void WireButton(Transform target, System.Action onClick)
        {
            if (target == null || onClick == null)
            {
                return;
            }

            EnsureCollider(target.gameObject, PreferSpriteSize(target));
            var button = target.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                onClick,
                BattleUiDimmerOverlay.CloseHitSort + 1,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.06f,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "CharacterSelectPanel.ButtonHover",
                    "character_select.button"));
        }

        private static void SetSelectFrameVisible(Transform slot, bool visible)
        {
            var frame = slot != null ? slot.Find(SelectFrameName) : null;
            if (frame != null && frame.gameObject.activeSelf != visible)
            {
                frame.gameObject.SetActive(visible);
            }
        }

        private static void WirePanelSwallow(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            var col = panel.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = panel.AddComponent<BoxCollider2D>();
            }

            var size = PreferSpriteSize(panel.transform);
            col.size = new Vector2(Mathf.Max(3f, size.x), Mathf.Max(3f, size.y));
            col.offset = Vector2.zero;
            col.isTrigger = false;
            col.enabled = true;

            var proxy = panel.GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = panel.AddComponent<UiOverlayHitProxy>();
            }

            proxy.Configure(
                UiOverlayHitAction.Swallow,
                BattleUiDimmerOverlay.HitSort + 1,
                PointerHitSurfacePriorities.Overlay);
        }

        private static BoxCollider2D EnsureCollider(GameObject go, Vector2 size)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider2D>();
            }

            col.size = size;
            col.offset = Vector2.zero;
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

        private static TMP_Text FindTmp(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == childName)
                {
                    return t.GetComponent<TMP_Text>();
                }
            }

            return null;
        }

        private static IGameFlowShellSystem ResolveShell()
        {
            return NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                   ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
        }

        private static CharacterSelectPanel FindSceneInstance()
        {
            var all = Resources.FindObjectsOfTypeAll<CharacterSelectPanel>();
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

        private static CharacterSelectPanel EnsureFromScene()
        {
            var root = FindSceneNamed(PanelRootName);
            if (root == null)
            {
                return null;
            }

            var panel = root.GetComponent<CharacterSelectPanel>();
            return panel != null ? panel : root.AddComponent<CharacterSelectPanel>();
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

        private static bool IsSceneObject(GameObject go)
        {
            return go != null && go.scene.IsValid() && go.scene.isLoaded;
        }
    }
}
