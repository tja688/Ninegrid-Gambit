using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Presentation.Ui
{
    /// <summary>
    /// 局终结算面板：胜负收口时展示本局数据（进度 / 金币 / 属性 / 遗物 / 角色），
    /// 等玩家点「返回主菜单」再放行 <c>EnterMainMenuImmediate</c>。
    /// 场景预置 <c>UI面板/结算面板BG</c>（默认失活）；缺预置时调用方回退旧 Notice 路径。
    /// 只读展示，不发 Core 指令（终端相位纪律）。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class RunSummaryPanel : MonoBehaviour
    {
        public const string PanelRootName = "结算面板BG";
        public const string TitleTextName = "标题文字";
        public const string SubtitleTextName = "副标题文字";
        public const string HeroNameTextName = "角色名字文字";
        public const string ProgressTextName = "数据_进度";
        public const string GoldTextName = "数据_金币";
        public const string InteractionTextName = "数据_互动";
        public const string StatsTextName = "数据_属性";
        public const string RelicTitleTextName = "遗物标题文字";
        public const string RelicIconAnchorName = "遗物图标区";
        public const string SeedTextName = "种子文字";
        public const string ReturnButtonName = "返回按钮";
        public const string VictoryDecorName = "胜利装饰";
        public const string DefeatDecorName = "失败装饰";

        private const string DimmerReason = "run-summary";
        private const string AvatarDefId = "avatar.default";
        private const int RelicIconsPerRow = 6;
        private const float RelicIconSpacing = 1.2f;

        private static RunSummaryPanel sInstance;

        private GameObject mPanelRoot;
        private TMP_Text mTitleText;
        private TMP_Text mSubtitleText;
        private TMP_Text mHeroNameText;
        private TMP_Text mProgressText;
        private TMP_Text mGoldText;
        private TMP_Text mInteractionText;
        private TMP_Text mStatsText;
        private TMP_Text mRelicTitleText;
        private TMP_Text mSeedText;
        private Transform mRelicIconAnchor;
        private bool mBound;
        private bool mDimmerHeld;
        private UniTaskCompletionSource mCloseTcs;

        public static bool IsOpen =>
            sInstance != null
            && sInstance.mPanelRoot != null
            && sInstance.mPanelRoot.activeSelf;

        /// <summary>
        /// 展示结算并阻塞到玩家确认返回；场景缺预置时返回 false（调用方回退旧 Notice）。
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
            live.Populate(victory);
            live.SetOpen(true);
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

            if (KeyboardUtility.GetKeyDown(KeyCode.Escape))
            {
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

            var uiRoot = FindSceneNamed("UI面板");
            BattleUiDimmerOverlay.EnsureBound(
                uiRoot != null ? uiRoot.transform.Find("半黑屏BG")?.gameObject : null);

            WirePanelSwallow(mPanelRoot);

            mTitleText = FindTmp(mPanelRoot.transform, TitleTextName);
            mSubtitleText = FindTmp(mPanelRoot.transform, SubtitleTextName);
            mHeroNameText = FindTmp(mPanelRoot.transform, HeroNameTextName);
            mProgressText = FindTmp(mPanelRoot.transform, ProgressTextName);
            mGoldText = FindTmp(mPanelRoot.transform, GoldTextName);
            mInteractionText = FindTmp(mPanelRoot.transform, InteractionTextName);
            mStatsText = FindTmp(mPanelRoot.transform, StatsTextName);
            mRelicTitleText = FindTmp(mPanelRoot.transform, RelicTitleTextName);
            mSeedText = FindTmp(mPanelRoot.transform, SeedTextName);
            mRelicIconAnchor = FindChild(mPanelRoot.transform, RelicIconAnchorName);

            WireButton(mPanelRoot.transform.Find(ReturnButtonName), RequestReturn);
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
                if (BattleUiDimmerOverlay.TryAcquire(DimmerReason))
                {
                    mDimmerHeld = true;
                }

                mPanelRoot.SetActive(true);
            }
            else
            {
                mPanelRoot.SetActive(false);
                ClearRelicIcons();
                if (mDimmerHeld)
                {
                    BattleUiDimmerOverlay.Release(DimmerReason);
                    mDimmerHeld = false;
                }
            }
        }

        private void RequestReturn()
        {
            InteractionAudioCues.Pulse(
                InteractionAudioCues.UiConfirm,
                "RunSummaryPanel.RequestReturn",
                "run_summary.return_main_menu");
            mCloseTcs?.TrySetResult();
        }

        private void Populate(bool victory)
        {
            var arch = NineGridArchitecture.Current;
            var run = arch?.GetModel<RunModel>();
            var player = arch?.GetModel<PlayerModel>();

            SetText(mTitleText, victory
                ? NineGrid.Core.Localization.L10n.Tr("summary.title_victory", "凯旋而归")
                : NineGrid.Core.Localization.L10n.Tr("summary.title_defeat", "壮志未酬"));
            if (mTitleText != null)
            {
                mTitleText.color = victory
                    ? new Color(0.95f, 0.82f, 0.42f, 1f)
                    : new Color(0.78f, 0.62f, 0.58f, 1f);
            }

            SetText(
                mSubtitleText,
                victory
                    ? NineGrid.Core.Localization.L10n.Tr(
                        "summary.subtitle_victory",
                        "你征服了全部三层地城，九宫的传说将铭记你的名字！")
                    : NineGrid.Core.Localization.L10n.Tr(
                        "summary.subtitle_defeat",
                        "地城的阴影暂时吞没了冒险者，重整旗鼓再来一局。"));

            SetDecorVisible(VictoryDecorName, victory);
            SetDecorVisible(DefeatDecorName, !victory);

            var heroName = NineGrid.Core.Localization.L10n.Tr("charselect.warrior_name", "战士");
            if (CardPresentationConfigCatalog.TryGet(AvatarDefId, out var dto)
                && dto != null
                && !string.IsNullOrWhiteSpace(dto.displayName))
            {
                heroName = dto.displayName;
            }

            SetText(mHeroNameText, heroName);

            if (run != null)
            {
                var floor = run.Floor != null ? run.Floor.Value : 1;
                var displayNode = MapNodeProgression.ToDisplayNode(
                    run.NodeIndex != null ? run.NodeIndex.Value : 0);
                SetText(
                    mProgressText,
                    victory
                        ? string.Format(
                            NineGrid.Core.Localization.L10n.Tr("summary.progress_victory", "通关进度　全 {0} 层制霸"),
                            RunModel.FinalFloor)
                        : string.Format(
                            NineGrid.Core.Localization.L10n.Tr("summary.progress_defeat", "通关进度　第 {0} 层 · 第 {1} 关"),
                            floor,
                            displayNode));
                SetText(mSeedText, string.Format(
                    NineGrid.Core.Localization.L10n.Tr("summary.seed", "本局种子 {0}"),
                    run.Seed?.Value ?? 0));
            }
            else
            {
                SetText(mProgressText, string.Empty);
                SetText(mSeedText, string.Empty);
            }

            SetText(
                mGoldText,
                player != null && player.Coins != null
                    ? string.Format(
                        NineGrid.Core.Localization.L10n.Tr("summary.gold", "持有金币　{0}"),
                        Mathf.Max(0, player.Coins.Value))
                    : string.Empty);
            SetText(
                mInteractionText,
                player != null && player.InteractionCount != null
                    ? string.Format(
                        NineGrid.Core.Localization.L10n.Tr("summary.interactions", "九宫互动　{0} 次"),
                        Mathf.Max(0, player.InteractionCount.Value))
                    : string.Empty);

            SetText(mStatsText, BuildAvatarStatsLine(arch));
            PopulateRelics(player);
        }

        private static string BuildAvatarStatsLine(QFramework.IArchitecture arch)
        {
            if (arch == null)
            {
                return string.Empty;
            }

            var board = arch.GetModel<BoardModel>();
            var avatarUid = board?.AvatarUid != null ? board.AvatarUid.Value : 0;
            if (avatarUid <= 0 || !arch.GetModel<CardRegistry>().TryGet(avatarUid, out var avatar))
            {
                return string.Empty;
            }

            var stats = arch.GetSystem<IStatSystem>();
            if (stats == null)
            {
                return string.Empty;
            }

            var hp = Mathf.Max(0, stats.GetEffectiveInt(avatar, StatId.Hp));
            var maxHp = Mathf.Max(hp, stats.GetEffectiveInt(avatar, StatId.MaxHp));
            var attack = Mathf.Max(0, stats.GetEffectiveInt(avatar, StatId.Attack));
            var armor = Mathf.Max(0, StatArmorUtility.GetEffectiveArmor(stats, avatar));
            return string.Format(
                NineGrid.Core.Localization.L10n.Tr(
                    "summary.stats",
                    "最终属性　生命 {0}/{1} · 攻击 {2} · 护甲 {3}"),
                hp,
                maxHp,
                attack,
                armor);
        }

        private void PopulateRelics(PlayerModel player)
        {
            ClearRelicIcons();
            var relicIds = player != null ? player.RelicDefIds : null;
            var count = relicIds != null ? relicIds.Count : 0;
            SetText(mRelicTitleText, string.Format(
                NineGrid.Core.Localization.L10n.Tr("summary.relics", "持有遗物　{0} 件"),
                count));

            if (mRelicIconAnchor == null || count == 0)
            {
                return;
            }

            var rootRenderer = mPanelRoot.GetComponent<SpriteRenderer>();
            var spawned = 0;
            for (var i = 0; i < relicIds.Count; i++)
            {
                var sprite = ResolveRelicSprite(relicIds[i]);
                if (sprite == null)
                {
                    continue;
                }

                var icon = new GameObject("遗物图标_" + relicIds[i]);
                icon.transform.SetParent(mRelicIconAnchor, worldPositionStays: false);
                var row = spawned / RelicIconsPerRow;
                var col = spawned % RelicIconsPerRow;
                icon.transform.localPosition = new Vector3(
                    col * RelicIconSpacing,
                    -row * RelicIconSpacing,
                    0f);
                icon.transform.localScale = Vector3.one;

                var renderer = icon.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (rootRenderer != null)
                {
                    renderer.sortingLayerID = rootRenderer.sortingLayerID;
                    renderer.sortingOrder = rootRenderer.sortingOrder + 3;
                    renderer.sharedMaterial = rootRenderer.sharedMaterial;
                }

                spawned++;
            }
        }

        private void ClearRelicIcons()
        {
            if (mRelicIconAnchor == null)
            {
                return;
            }

            for (var i = mRelicIconAnchor.childCount - 1; i >= 0; i--)
            {
                Destroy(mRelicIconAnchor.GetChild(i).gameObject);
            }
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

        private void SetDecorVisible(string decorName, bool visible)
        {
            var decor = FindChild(mPanelRoot.transform, decorName);
            if (decor != null && decor.gameObject.activeSelf != visible)
            {
                decor.gameObject.SetActive(visible);
            }
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
                    "RunSummaryPanel.ButtonHover",
                    "run_summary.button"));
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
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

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == childName)
                {
                    return t;
                }
            }

            return null;
        }

        private static TMP_Text FindTmp(Transform root, string childName)
        {
            var child = FindChild(root, childName);
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
