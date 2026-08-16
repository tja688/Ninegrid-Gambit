using System;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 选人界面：主菜单「开始游戏」后弹出，纯黑屏底幕（<see cref="PureBlackScreenOverlay"/>）。
    /// 场景预置 <c>UI面板/选人界面BG</c>（默认失活）：
    /// - 角色1 = 战士（会动的立绘 + 专属遗物图标），点击立绘切换选中；专属遗物右键开详述；
    /// - 角色2/3 = 未解锁席位（Layla / Icey 黑色剪影 + 「尚未实装」文案，点击拒绝）；
    /// - 「开始游戏」在已选解锁角色时正式出发（<see cref="GameFlowController.BeginFormalRun"/>）；
    /// - 难度选项三档均可点选（当前全部路由普通数据，仅记录到 <see cref="RunSetupSelection"/>）；
    /// - 「回到主菜单 (1)」直接返回不提示，小字说明悬停才出现。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CharacterSelectPanel : MonoBehaviour
    {
        public const string PanelRootName = "选人界面BG";
        public const string StartButtonName = "开始游戏按钮";
        public const string BackButtonName = "回到主菜单 (1)";
        public const string PortraitNodeName = "立绘";
        private const string HighlightChildName = "__Highlight";

        private const string OverlayReason = "character-select";
        private const string WarriorIdleKey =
            "ContentArt/Multiple/MonstersAndHumans/SpriteSheets(96x96)/Human_Soldier_Sword_Shield/No_Shadows/Human_Soldier_Sword_Shield_Idle-Sheet";
        private const string LockedIdleKeyA = "ContentArt/Png/像素怪物合集/sprites/Layla_idle_01";
        private const string LockedIdleKeyB = "ContentArt/Png/像素怪物合集/sprites/Icey_idle_01";

        // 战士序列帧 96x96 里主体只占中间一小块，帧高给大些才与剪影视觉均衡（Play 实测校准）。
        private const float WarriorPortraitWorldHeight = 5.6f;
        private const float LockedPortraitWorldHeight = 1.9f;
        private const float ItemIconWorldHeight = 0.9f;
        private const int PortraitSortingOrder = 40;
        private const int ItemIconSortingOrder = 16;
        private const float LockedFlashSeconds = 0.9f;

        private static readonly Color SilhouetteTint = new Color(0.05f, 0.05f, 0.07f, 1f);
        private static readonly Color DifficultyIdleTint = new Color(0.5f, 0.5f, 0.5f, 0.9f);
        private static readonly Color LockedFlashColor = new Color(1f, 0.72f, 0.35f, 1f);

        private static CharacterSelectPanel sInstance;

        private sealed class CharacterSlot
        {
            public Transform Root;
            public SpriteRenderer PortraitArt;
            public SpriteRenderer PortraitHighlight;
            public TMP_Text Description;
            public Color DescriptionBaseColor;
            public bool Unlocked;
            public string RelicDefId;
        }

        private sealed class DifficultyOption
        {
            public Transform Node;
            public SpriteRenderer Icon;
            public TMP_Text Label;
            public float LabelBaseAlpha = 1f;
            public string Id;
            public string DisplayName;
        }

        private GameObject mPanelRoot;
        private readonly CharacterSlot[] mSlots = new CharacterSlot[3];
        private readonly DifficultyOption[] mDifficulties = new DifficultyOption[3];
        private TMP_Text mBackLabel;
        private int mSelectedSlotIndex = -1;
        private int mSelectedDifficulty;
        private bool mBound;
        private bool mOverlayHeld;
        private bool mDeparting;
        private float mLockedFlashClearAt = -1f;
        private TMP_Text mLockedFlashText;

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
            if (mOverlayHeld)
            {
                PureBlackScreenOverlay.Release(OverlayReason);
                mOverlayHeld = false;
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

            if (mLockedFlashClearAt > 0f && Time.unscaledTime >= mLockedFlashClearAt)
            {
                mLockedFlashClearAt = -1f;
                RestoreLockedFlash();
            }

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                Back();
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
            // 失活节点 AddComponent 不触发 Awake：先归位实例状态再接线。
            EnsureInstanceState();
            if (mBound)
            {
                return;
            }

            WireCharacterSlot(0, "角色1", unlocked: true);
            WireCharacterSlot(1, "角色2", unlocked: false);
            WireCharacterSlot(2, "角色3", unlocked: false);
            WireDifficulty(0, "难度选项：普通", RunSetupSelection.NormalDifficultyId, "普通");
            WireDifficulty(1, "难度选项：进阶", "advanced", "进阶");
            WireDifficulty(2, "难度选项：困难", "hard", "困难");
            WireStartButton();
            WireBackButton();

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
                SetMainMenuChromeVisible(false);

                if (PureBlackScreenOverlay.Acquire(OverlayReason))
                {
                    mOverlayHeld = true;
                }

                mDeparting = false;
                mPanelRoot.SetActive(true);
                RefreshOpenVisuals();
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiConfirm,
                    "CharacterSelectPanel.SetOpen",
                    "character_select.open");
            }
            else
            {
                RestoreLockedFlash();
                mLockedFlashClearAt = -1f;
                mPanelRoot.SetActive(false);
                if (mOverlayHeld)
                {
                    PureBlackScreenOverlay.Release(OverlayReason);
                    mOverlayHeld = false;
                }

                if (!mDeparting)
                {
                    SetMainMenuChromeVisible(true);
                }
            }
        }

        /// <summary>选人叠层打开时收起主菜单按钮/立绘，避免 UI 层元素压在纯黑幕之上。</summary>
        private static void SetMainMenuChromeVisible(bool visible)
        {
            var router = UnityEngine.Object.FindFirstObjectByType<UiPanelRouter>();
            router?.EnsureBindings();
            var panel = router != null ? router.MainPanel : null;
            if (panel != null && panel.activeSelf != visible)
            {
                panel.SetActive(visible);
            }
        }

        /// <summary>每次打开：装载立绘/图标、难度回默认普通、隐藏返回小字。</summary>
        private void RefreshOpenVisuals()
        {
            for (var i = 0; i < mSlots.Length; i++)
            {
                var slot = mSlots[i];
                if (slot?.PortraitArt == null)
                {
                    continue;
                }

                PanelArtUtility.FitWorldHeight(
                    slot.PortraitArt,
                    slot.Unlocked ? WarriorPortraitWorldHeight : LockedPortraitWorldHeight);
                SyncPortraitCollider(slot);
            }

            SelectDifficulty(0, silent: true);
            SelectCharacter(FindFirstUnlockedSlotIndex(), silent: true);
            SetBackLabelVisible(false);
        }

        private void WireCharacterSlot(int index, string rootName, bool unlocked)
        {
            var root = FindDirectChild(transform, rootName);
            if (root == null)
            {
                Debug.LogWarning("[CharacterSelect] 缺少角色席位节点：" + rootName);
                return;
            }

            var slot = new CharacterSlot { Root = root, Unlocked = unlocked };

            // 立绘：__Art 序列帧（战士 = 本色；未解锁 = 黑色剪影）。
            var portrait = FindDirectChild(root, PortraitNodeName);
            if (portrait != null)
            {
                var key = index == 1 ? LockedIdleKeyA : (index == 2 ? LockedIdleKeyB : WarriorIdleKey);
                slot.PortraitArt = PanelArtUtility.EnsureLoopArt(
                    portrait,
                    key,
                    unlocked ? Color.white : SilhouetteTint,
                    PortraitSortingOrder);

                slot.PortraitHighlight = portrait.Find(HighlightChildName)?.GetComponent<SpriteRenderer>();
                WirePortraitButton(portrait, slot);
            }

            // 角色描述：解锁文案 / 「尚未实装，敬请期待」。
            slot.Description = FindChildTmpByPrefix(root, "角色描述");
            if (slot.Description != null)
            {
                slot.DescriptionBaseColor = slot.Description.color;
            }

            // 角色专属道具卡：战士显示初始遗物图标；未解锁席位留空。
            var itemCard = FindDirectChildByPrefix(root, "角色专属道具卡");
            if (itemCard != null)
            {
                if (unlocked)
                {
                    slot.RelicDefId = ResolveSlotRelicDefId(index);
                    var relicSprite = ResolveRelicSprite(slot.RelicDefId);
                    var art = PanelArtUtility.SetStaticArt(itemCard, relicSprite, ItemIconSortingOrder);
                    PanelArtUtility.FitWorldHeight(art, ItemIconWorldHeight);
                    WireRelicInspect(itemCard, slot.RelicDefId);
                }
                else
                {
                    slot.RelicDefId = null;
                    PanelArtUtility.SetStaticArt(itemCard, null, ItemIconSortingOrder);
                    DisableSlotCollider(itemCard);
                }
            }

            mSlots[index] = slot;
        }

        private void WirePortraitButton(Transform portrait, CharacterSlot slot)
        {
            var collider = portrait.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = portrait.gameObject.AddComponent<BoxCollider2D>();
            }

            collider.size = new Vector2(1.4f, 1.4f);
            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.enabled = true;

            var button = portrait.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = portrait.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                () => OnCharacterClicked(slot),
                BattleUiDimmerOverlay.CloseHitSort + 1,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: slot.Unlocked ? 1.05f : 1f,
                onHoverEnter: slot.Unlocked
                    ? () => InteractionAudioCues.Pulse(
                        InteractionAudioCues.MainMenuHover,
                        "CharacterSelectPanel.PortraitHover",
                        "character_select.portrait")
                    : (Action)null);
        }

        /// <summary>点击盒固定为角色主体大小（世界约 1.7×2.0），不随帧空白放大。</summary>
        private static void SyncPortraitCollider(CharacterSlot slot)
        {
            if (slot?.PortraitArt == null)
            {
                return;
            }

            var portrait = slot.PortraitArt.transform.parent;
            var collider = portrait != null ? portrait.GetComponent<BoxCollider2D>() : null;
            if (collider == null)
            {
                return;
            }

            var lossy = portrait.lossyScale;
            collider.size = new Vector2(
                1.7f / Mathf.Max(0.01f, Mathf.Abs(lossy.x)),
                2.0f / Mathf.Max(0.01f, Mathf.Abs(lossy.y)));
            collider.offset = Vector2.zero;
        }

        private void OnCharacterClicked(CharacterSlot slot)
        {
            if (slot == null || !IsOpen)
            {
                return;
            }

            if (!slot.Unlocked)
            {
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuReject,
                    "CharacterSelectPanel.OnCharacterClicked",
                    "character_select.locked");
                FlashLockedDescription(slot);
                return;
            }

            var index = FindSlotIndex(slot);
            if (index >= 0)
            {
                SelectCharacter(index, silent: false);
            }
        }

        private void OnStartClicked()
        {
            if (!IsOpen || mDeparting)
            {
                return;
            }

            var slot = GetSelectedSlot();
            if (slot == null || !slot.Unlocked)
            {
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuReject,
                    "CharacterSelectPanel.OnStartClicked",
                    "character_select.start_locked");
                return;
            }

            mDeparting = true;
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "CharacterSelectPanel.OnStartClicked",
                "character_select.depart");
            SetOpen(false);

            var controller = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            if (controller != null)
            {
                controller.BeginFormalRun();
                return;
            }

            Debug.LogWarning("[CharacterSelect] 未找到 GameFlowController，无法开局。");
        }

        private void SelectCharacter(int index, bool silent)
        {
            if (index < 0 || index >= mSlots.Length || mSlots[index] == null || !mSlots[index].Unlocked)
            {
                return;
            }

            mSelectedSlotIndex = index;
            RefreshSelectionVisuals();

            if (!silent)
            {
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiPress,
                    "CharacterSelectPanel.SelectCharacter",
                    "character_select.slot." + index);
            }
        }

        private void RefreshSelectionVisuals()
        {
            for (var i = 0; i < mSlots.Length; i++)
            {
                var slot = mSlots[i];
                if (slot?.PortraitHighlight == null)
                {
                    continue;
                }

                var selected = i == mSelectedSlotIndex;
                slot.PortraitHighlight.enabled = selected;
            }
        }

        private CharacterSlot GetSelectedSlot()
        {
            if (mSelectedSlotIndex < 0 || mSelectedSlotIndex >= mSlots.Length)
            {
                return null;
            }

            return mSlots[mSelectedSlotIndex];
        }

        private int FindSlotIndex(CharacterSlot slot)
        {
            for (var i = 0; i < mSlots.Length; i++)
            {
                if (mSlots[i] == slot)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindFirstUnlockedSlotIndex()
        {
            for (var i = 0; i < mSlots.Length; i++)
            {
                if (mSlots[i] != null && mSlots[i].Unlocked)
                {
                    return i;
                }
            }

            return 0;
        }

        private void FlashLockedDescription(CharacterSlot slot)
        {
            RestoreLockedFlash();
            if (slot.Description == null)
            {
                return;
            }

            mLockedFlashText = slot.Description;
            mLockedFlashText.color = LockedFlashColor;
            mLockedFlashClearAt = Time.unscaledTime + LockedFlashSeconds;
        }

        private void RestoreLockedFlash()
        {
            if (mLockedFlashText != null)
            {
                var slot = FindSlotByDescription(mLockedFlashText);
                mLockedFlashText.color = slot != null
                    ? slot.DescriptionBaseColor
                    : Color.white;
                mLockedFlashText = null;
            }
        }

        private CharacterSlot FindSlotByDescription(TMP_Text text)
        {
            for (var i = 0; i < mSlots.Length; i++)
            {
                if (mSlots[i] != null && mSlots[i].Description == text)
                {
                    return mSlots[i];
                }
            }

            return null;
        }

        private void WireDifficulty(int index, string nodeName, string id, string displayName)
        {
            var node = FindDirectChild(transform, nodeName);
            if (node == null)
            {
                Debug.LogWarning("[CharacterSelect] 缺少难度选项节点：" + nodeName);
                return;
            }

            var option = new DifficultyOption
            {
                Node = node,
                Icon = node.GetComponent<SpriteRenderer>(),
                Label = node.GetComponentInChildren<TMP_Text>(true),
                Id = id,
                DisplayName = displayName,
            };
            if (option.Label != null)
            {
                option.LabelBaseAlpha = option.Label.color.a;
            }

            EnsureButtonCollider(node);
            var button = node.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = node.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                () => SelectDifficulty(index, silent: false),
                BattleUiDimmerOverlay.CloseHitSort + 1,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.06f,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "CharacterSelectPanel.DifficultyHover",
                    "character_select.difficulty"));

            mDifficulties[index] = option;
        }

        private void SelectDifficulty(int index, bool silent)
        {
            if (index < 0 || index >= mDifficulties.Length || mDifficulties[index] == null)
            {
                return;
            }

            mSelectedDifficulty = index;
            var chosen = mDifficulties[index];
            RunSetupSelection.SetDifficulty(
                chosen.Id,
                chosen.DisplayName,
                chosen.Icon != null ? chosen.Icon.sprite : null);

            for (var i = 0; i < mDifficulties.Length; i++)
            {
                var option = mDifficulties[i];
                if (option == null)
                {
                    continue;
                }

                var selected = i == mSelectedDifficulty;
                if (option.Icon != null)
                {
                    option.Icon.color = selected ? Color.white : DifficultyIdleTint;
                }

                if (option.Label != null)
                {
                    var c = option.Label.color;
                    c.a = selected ? option.LabelBaseAlpha : option.LabelBaseAlpha * 0.45f;
                    option.Label.color = c;
                }
            }

            if (!silent)
            {
                InteractionAudioCues.Pulse(
                    InteractionAudioCues.UiPress,
                    "CharacterSelectPanel.SelectDifficulty",
                    "character_select.difficulty." + chosen.Id);
            }
        }

        private void WireStartButton()
        {
            var start = FindDirectChild(transform, StartButtonName)
                        ?? FindDirectChildByPrefix(transform, "开始游戏");
            if (start == null)
            {
                Debug.LogWarning("[CharacterSelect] 缺少开始按钮：" + StartButtonName);
                return;
            }

            EnsureButtonCollider(start);
            var button = start.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = start.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                OnStartClicked,
                BattleUiDimmerOverlay.CloseHitSort + 2,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.08f,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "CharacterSelectPanel.StartHover",
                    "character_select.start"));
        }

        private void WireBackButton()
        {
            var back = FindDirectChild(transform, BackButtonName);
            if (back == null)
            {
                Debug.LogWarning("[CharacterSelect] 缺少返回按钮：" + BackButtonName);
                return;
            }

            mBackLabel = back.GetComponentInChildren<TMP_Text>(true);
            SetBackLabelVisible(false);

            EnsureButtonCollider(back);
            var button = back.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = back.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                Back,
                BattleUiDimmerOverlay.CloseHitSort + 2,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.08f,
                onHoverEnter: () =>
                {
                    SetBackLabelVisible(true);
                    InteractionAudioCues.Pulse(
                        InteractionAudioCues.MainMenuHover,
                        "CharacterSelectPanel.BackHover",
                        "character_select.back");
                },
                onHoverExit: () => SetBackLabelVisible(false));
        }

        private void SetBackLabelVisible(bool visible)
        {
            if (mBackLabel != null && mBackLabel.gameObject.activeSelf != visible)
            {
                mBackLabel.gameObject.SetActive(visible);
            }
        }

        /// <summary>返回主菜单：按需求默认不弹提示，直接收起。</summary>
        private void Back()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiCancel,
                "CharacterSelectPanel.Back",
                "character_select.back");
            SetOpen(false);
        }

        private static string ResolveSlotRelicDefId(int slotIndex)
        {
            if (slotIndex != 0)
            {
                return null;
            }

            return ProfessionCatalog.Default != null
                ? ProfessionCatalog.Default.InitialRelicDefId
                : null;
        }

        private static void WireRelicInspect(Transform itemCard, string relicDefId)
        {
            if (itemCard == null || string.IsNullOrEmpty(relicDefId))
            {
                return;
            }

            var proxy = itemCard.GetComponent<ContentIconSlotHitProxy>();
            if (proxy == null)
            {
                proxy = itemCard.gameObject.AddComponent<ContentIconSlotHitProxy>();
            }

            proxy.DefId = relicDefId;

            var collider = itemCard.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = itemCard.gameObject.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            collider.enabled = true;
            if (collider.size.sqrMagnitude < 0.01f)
            {
                var renderer = itemCard.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                {
                    collider.size = renderer.sprite.bounds.size;
                }
                else
                {
                    collider.size = new Vector2(0.8f, 0.8f);
                }
            }
        }

        private static Sprite ResolveRelicSprite(string relicDefId)
        {
            if (string.IsNullOrEmpty(relicDefId))
            {
                return null;
            }

            if (CardPresentationConfigCatalog.TryGet(relicDefId, out var dto)
                && dto?.sprites != null
                && !string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                var sprite = CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return ContentIconSlotBinder.TryLoadLegacyRelicIconPublic(relicDefId);
        }

        private static void EnsureButtonCollider(Transform node)
        {
            var collider = node.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = node.gameObject.AddComponent<BoxCollider2D>();
            }

            var renderer = node.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                var size = renderer.drawMode == SpriteDrawMode.Sliced
                    ? renderer.size
                    : (Vector2)renderer.sprite.bounds.size;
                if (size.x > 0.01f && size.y > 0.01f)
                {
                    collider.size = size;
                }
            }
            else if (collider.size.x < 0.01f || collider.size.y < 0.01f)
            {
                collider.size = new Vector2(0.5f, 0.5f);
            }

            collider.offset = Vector2.zero;
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private static void DisableSlotCollider(Transform node)
        {
            var collider = node.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>部分场景节点名带尾随空格 / 编号（如「角色专属道具卡 」），按前缀匹配。</summary>
        private static Transform FindDirectChildByPrefix(Transform parent, string prefix)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name.TrimEnd().StartsWith(prefix, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static TMP_Text FindChildTmpByPrefix(Transform parent, string prefix)
        {
            var child = FindDirectChildByPrefix(parent, prefix);
            return child != null ? child.GetComponent<TMP_Text>() : null;
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
