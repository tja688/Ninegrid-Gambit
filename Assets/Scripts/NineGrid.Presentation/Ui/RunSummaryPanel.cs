using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
    /// 完结结算面板：胜负收口时以纯黑屏底幕展示本局数据，等玩家选择去向再放行。
    /// 场景预置 <c>UI面板/完结结算BG</c>（默认失活）：
    /// - 会动的玩家立绘 + 本局所选难度图标 + 本局遗物墙（12 格占位）；
    /// - 右侧文字统计（所用时长 / 击败怪物数 / 损失血量 / 使用道具卡数，数据源 <see cref="RunRecapTracker"/>）；
    /// - 「回到主菜单」「退出游戏」走提示框二次确认；「再来一局」直接回到选人界面重开。
    /// 只读展示，不发 Core 指令（终端相位纪律）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class RunSummaryPanel : MonoBehaviour
    {
        public const string PanelRootName = "完结结算BG";
        public const string TitleTextName = "房间信息";
        public const string StatsBodyTextName = "房间信息2 (1)";
        public const string CharacterInfoName = "角色信息";
        public const string PortraitNodeName = "立绘";
        public const string DifficultyIconName = "此次对局所选的难度";
        public const string DifficultyTextName = "难度文本";
        public const string RelicWallName = "此次对局所使用的遗物";
        public const string QuitButtonName = "退出游戏";
        public const string RestartButtonName = "再来一局";
        public const string ReturnButtonName = "回到主菜单";

        private const string OverlayReason = "run-summary";
        private const string WarriorIdleKey =
            "ContentArt/Multiple/MonstersAndHumans/SpriteSheets(96x96)/Human_Soldier_Sword_Shield/No_Shadows/Human_Soldier_Sword_Shield_Idle-Sheet";
        // 战士序列帧主体只占 96x96 一小块，帧高按选人界面同参（Play 实测校准）。
        private const float PortraitWorldHeight = 5.6f;
        private const float RelicIconWorldHeight = 0.72f;
        private const int PortraitSortingOrder = 40;
        private const int RelicIconSortingOrder = 24;

        private static readonly Color VictoryTitleColor = new Color(0.95f, 0.82f, 0.42f, 1f);
        private static readonly Color DefeatTitleColor = new Color(0.82f, 0.55f, 0.5f, 1f);

        private static RunSummaryPanel sInstance;

        private GameObject mPanelRoot;
        private TMP_Text mTitleText;
        private TMP_Text mStatsBodyText;
        private SpriteRenderer mPortraitArt;
        private SpriteRenderer mDifficultyIcon;
        private TMP_Text mDifficultyText;
        private readonly List<Transform> mRelicSlots = new List<Transform>(12);
        private UiConfirmPrompt mPrompt;
        private bool mBound;
        private bool mOverlayHeld;
        private UniTaskCompletionSource mCloseTcs;

        public static bool IsOpen =>
            sInstance != null
            && sInstance.mPanelRoot != null
            && sInstance.mPanelRoot.activeSelf;

        /// <summary>
        /// 展示结算并阻塞到玩家确认去向；场景缺预置时返回 false（调用方回退旧 Notice）。
        /// 取消（强退回菜单）时面板自动收起。
        /// </summary>
        public static async UniTask<bool> TryShowAndWaitAsync(bool victory, CancellationToken ct)
        {
            var live = FindSceneInstance() ?? EnsureFromScene();
            if (live == null)
            {
                return false;
            }

            live.EnsureBound();
            live.SetOpen(true);
            live.Populate(victory);
            live.mCloseTcs = new UniTaskCompletionSource();
            try
            {
                await live.mCloseTcs.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                live.mCloseTcs = null;
                live.SetOpen(false);
            }

            return true;
        }

        public static void CloseIfOpen()
        {
            if (sInstance == null)
            {
                return;
            }

            sInstance.mCloseTcs?.TrySetResult();
            sInstance.SetOpen(false);
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

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
                // 提示框开着时由它消费 Esc；同帧刚被消费也不再叠加。
                if (UiConfirmPrompt.IsAnyOpen || UiConfirmPrompt.EscapeHandledThisFrame)
                {
                    return;
                }

                RequestReturn();
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
            EnsureInstanceState();
            if (mBound)
            {
                return;
            }

            mTitleText = FindTmp(mPanelRoot.transform, TitleTextName);
            mStatsBodyText = FindTmp(mPanelRoot.transform, StatsBodyTextName);

            var characterInfo = FindDirectChild(mPanelRoot.transform, CharacterInfoName);
            if (characterInfo != null)
            {
                var portrait = FindDirectChild(characterInfo, PortraitNodeName);
                if (portrait != null)
                {
                    mPortraitArt = PanelArtUtility.EnsureLoopArt(
                        portrait,
                        WarriorIdleKey,
                        Color.white,
                        PortraitSortingOrder);
                    DisableSlotCollider(portrait);
                }

                var difficulty = FindDirectChild(characterInfo, DifficultyIconName);
                if (difficulty != null)
                {
                    mDifficultyIcon = difficulty.GetComponent<SpriteRenderer>();
                    mDifficultyText = FindTmp(difficulty, DifficultyTextName)
                        ?? difficulty.GetComponentInChildren<TMP_Text>(true);
                }
                else
                {
                    mDifficultyIcon = null;
                    mDifficultyText = null;
                }

                CollectRelicSlots(FindDirectChild(characterInfo, RelicWallName));
            }

            mPrompt = UiConfirmPrompt.Attach(FindDirectChild(mPanelRoot.transform, UiConfirmPrompt.NodeName));

            WireButton(FindDirectChild(mPanelRoot.transform, ReturnButtonName), RequestReturn);
            WireButton(FindDirectChild(mPanelRoot.transform, QuitButtonName), RequestQuit);
            WireButton(FindDirectChild(mPanelRoot.transform, RestartButtonName), RequestRestart);

            mBound = true;
        }

        private void SetOpen(bool open)
        {
            if (mPanelRoot == null || open == mPanelRoot.activeSelf)
            {
                return;
            }

            if (open)
            {
                if (PureBlackScreenOverlay.Acquire(OverlayReason))
                {
                    mOverlayHeld = true;
                }

                mPanelRoot.SetActive(true);
            }
            else
            {
                mPanelRoot.SetActive(false);
                ClearRelicIcons();
                if (mOverlayHeld)
                {
                    PureBlackScreenOverlay.Release(OverlayReason);
                    mOverlayHeld = false;
                }
            }
        }

        private void Populate(bool victory)
        {
            RunRecapTracker.ScanNow();

            SetText(mTitleText, victory
                ? NineGrid.Core.Localization.L10n.Tr("summary.title_victory", "胜利！！！！")
                : NineGrid.Core.Localization.L10n.Tr("summary.title_defeat", "失败……"));
            if (mTitleText != null)
            {
                mTitleText.color = victory ? VictoryTitleColor : DefeatTitleColor;
            }

            if (mPortraitArt != null)
            {
                PanelArtUtility.FitWorldHeight(mPortraitArt, PortraitWorldHeight);
            }

            var run = NineGridArchitecture.Current?.GetModel<RunModel>();
            var difficultyId = run?.DifficultyId?.Value ?? RunSetupSelection.DifficultyId;
            var difficultyLabel = !string.IsNullOrEmpty(RunSetupSelection.DifficultyLabel)
                ? RunSetupSelection.DifficultyLabel
                : RunSetupSelection.GetDefaultLabel(difficultyId);

            if (mDifficultyIcon != null)
            {
                var icon = RunSetupSelection.DifficultyIcon ?? ResolveDifficultyIconFallback(difficultyId);
                if (icon != null)
                {
                    mDifficultyIcon.sprite = icon;
                }
            }

            if (mDifficultyText != null)
            {
                mDifficultyText.text = $"难度：{difficultyLabel}";
            }

            PopulateStats();
            PopulateRelics();
        }

        private void PopulateStats()
        {
            if (mStatsBodyText == null)
            {
                return;
            }

            mStatsBodyText.text = string.Format(
                NineGrid.Core.Localization.L10n.Tr(
                    "summary.stats_body",
                    "所用时长：{0}\n\n击败的怪物数量：{1}\n\n损失血量：{2}\n\n使用道具卡数量：{3}"),
                RunRecapTracker.FormatElapsed(),
                RunRecapTracker.MonstersKilled,
                RunRecapTracker.HpLost,
                RunRecapTracker.ItemCardsUsed);
        }

        private void PopulateRelics()
        {
            ClearRelicIcons();
            var player = NineGridArchitecture.Current?.GetModel<PlayerModel>();
            var relicIds = player != null ? player.RelicDefIds : null;
            var count = relicIds != null ? relicIds.Count : 0;

            var filled = 0;
            for (var i = 0; i < count && filled < mRelicSlots.Count; i++)
            {
                var sprite = ResolveRelicSprite(relicIds[i]);
                if (sprite == null)
                {
                    continue;
                }

                var art = PanelArtUtility.SetStaticArt(mRelicSlots[filled], sprite, RelicIconSortingOrder);
                PanelArtUtility.FitWorldHeight(art, RelicIconWorldHeight);
                filled++;
            }
        }

        private void ClearRelicIcons()
        {
            for (var i = 0; i < mRelicSlots.Count; i++)
            {
                PanelArtUtility.SetStaticArt(mRelicSlots[i], null, RelicIconSortingOrder);
            }
        }

        /// <summary>遗物墙 12 个占位槽：按视觉顺序（先上行后下行、从左到右）排序。</summary>
        private void CollectRelicSlots(Transform wall)
        {
            mRelicSlots.Clear();
            if (wall == null)
            {
                return;
            }

            for (var i = 0; i < wall.childCount; i++)
            {
                var child = wall.GetChild(i);
                if (child != null && child.name.StartsWith("占位模板", StringComparison.Ordinal))
                {
                    mRelicSlots.Add(child);
                    DisableSlotCollider(child);
                }
            }

            mRelicSlots.Sort((a, b) =>
            {
                var ay = a.localPosition.y;
                var by = b.localPosition.y;
                if (Mathf.Abs(ay - by) > 0.05f)
                {
                    return by.CompareTo(ay);
                }

                return a.localPosition.x.CompareTo(b.localPosition.x);
            });
        }

        private void RequestReturn()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "RunSummaryPanel.RequestReturn",
                "run_summary.return_main_menu");
            if (mPrompt != null)
            {
                mPrompt.Show(UiConfirmPrompt.ReturnMainMenuMessage, CompleteReturn);
                return;
            }

            CompleteReturn();
        }

        private void CompleteReturn()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "RunSummaryPanel.CompleteReturn",
                "run_summary.return_main_menu");
            mCloseTcs?.TrySetResult();
        }

        private void RequestQuit()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiPress,
                "RunSummaryPanel.RequestQuit",
                "run_summary.quit_game");
            if (mPrompt != null)
            {
                mPrompt.Show(UiConfirmPrompt.QuitGameMessage, CompleteQuit);
                return;
            }

            CompleteQuit();
        }

        private void CompleteQuit()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.MainMenuCancel,
                "RunSummaryPanel.CompleteQuit",
                "run_summary.quit_game");
            var controller = UnityEngine.Object.FindFirstObjectByType<GameFlowController>();
            if (controller != null)
            {
                controller.QuitGame();
                return;
            }

            Application.Quit();
        }

        /// <summary>再来一局：收面板放行回主菜单收口，随后自动打开选人界面重开。</summary>
        private void RequestRestart()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "RunSummaryPanel.RequestRestart",
                "run_summary.restart");
            mCloseTcs?.TrySetResult();
            OpenCharacterSelectAfterReturnAsync().Forget();
        }

        private static async UniTaskVoid OpenCharacterSelectAfterReturnAsync()
        {
            var shell = NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                        ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
            var deadline = Time.realtimeSinceStartup + 8f;
            while (shell != null
                   && (shell.IsBusy || shell.State.Value != GameFlowShellState.MainMenu)
                   && Time.realtimeSinceStartup < deadline)
            {
                await UniTask.Yield();
            }

            // 收口后再让一帧，避开 EnterMainMenu 的同帧表现清理。
            await UniTask.Yield();
            if (shell != null && shell.State.Value != GameFlowShellState.MainMenu)
            {
                Debug.LogWarning("[RunSummary] 再来一局：主菜单收口超时，放弃自动打开选人界面。");
                return;
            }

            CharacterSelectPanel.RequestOpen();
        }

        private void WireButton(Transform target, Action onClick)
        {
            if (target == null || onClick == null)
            {
                return;
            }

            EnsureButtonCollider(target);
            var button = target.GetComponent<WorldUiHitButton>();
            if (button == null)
            {
                button = target.gameObject.AddComponent<WorldUiHitButton>();
            }

            button.Configure(
                onClick,
                BattleUiDimmerOverlay.CloseHitSort + 1,
                PointerHitSurfacePriorities.Overlay,
                hoverScale: 1.08f,
                onHoverEnter: () => InteractionAudioCues.Pulse(
                    InteractionAudioCues.MainMenuHover,
                    "RunSummaryPanel.ButtonHover",
                    "run_summary.button"));
        }

        private static Sprite ResolveRelicSprite(string defId)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return null;
            }

            if (CardPresentationConfigCatalog.TryGet(defId, out var dto)
                && dto?.sprites != null
                && !string.IsNullOrWhiteSpace(dto.sprites.mainIcon))
            {
                var fromJson = CardPresentationSpritePath.LoadSprite(dto.sprites.mainIcon);
                if (fromJson != null)
                {
                    return fromJson;
                }
            }

            return ContentIconSlotBinder.TryLoadLegacyRelicIconPublic(defId);
        }

        private static void EnsureButtonCollider(Transform node)
        {
            var collider = node.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = node.gameObject.AddComponent<BoxCollider2D>();
            }

            if (collider.size.x < 0.01f || collider.size.y < 0.01f)
            {
                var renderer = node.GetComponent<SpriteRenderer>();
                var size = renderer != null && renderer.sprite != null
                    ? (renderer.drawMode == SpriteDrawMode.Simple
                        ? (Vector2)renderer.sprite.bounds.size
                        : renderer.size)
                    : new Vector2(0.5f, 0.5f);
                collider.size = size;
            }

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

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }

        private static Sprite ResolveDifficultyIconFallback(string difficultyId)
        {
            var targetNodeName = string.Equals(difficultyId, NineGrid.Core.Content.RunDifficultyIds.Hard, StringComparison.OrdinalIgnoreCase)
                ? "难度选项：困难"
                : (string.Equals(difficultyId, NineGrid.Core.Content.RunDifficultyIds.Advanced, StringComparison.OrdinalIgnoreCase)
                    ? "难度选项：进阶"
                    : "难度选项：普通");

            var panel = FindSceneNamed(CharacterSelectPanel.PanelRootName);
            if (panel != null)
            {
                var node = FindDirectChild(panel.transform, targetNodeName);
                var sr = node != null ? node.GetComponent<SpriteRenderer>() : null;
                if (sr != null && sr.sprite != null)
                {
                    return sr.sprite;
                }
            }

            return null;
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

        private static TMP_Text FindTmp(Transform root, string childName)
        {
            var child = FindDirectChild(root, childName);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static RunSummaryPanel FindSceneInstance()
        {
            var all = Resources.FindObjectsOfTypeAll<RunSummaryPanel>();
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

        private static RunSummaryPanel EnsureFromScene()
        {
            var root = FindSceneNamed(PanelRootName);
            if (root == null)
            {
                return null;
            }

            var panel = root.GetComponent<RunSummaryPanel>();
            return panel != null ? panel : root.AddComponent<RunSummaryPanel>();
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
