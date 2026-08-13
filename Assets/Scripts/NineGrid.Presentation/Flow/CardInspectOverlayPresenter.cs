using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation;
using QFramework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Flow
{
    /// <summary>
    /// 右键卡牌详述面板：敌方 / 常规两态，半黑屏挡交互，不暂停主线。
    /// 场景里的「敌人卡模板占位」「道具卡标准模版」只作锚点；成品 mock 图会被关掉，
    /// 运行时在锚点下挂真卡面预制体并 Commit。
    /// 常规占位按道具卡（卡框本地原点）布局；遗物卡面内容偏左时由
    /// <see cref="AlignLiveFaceToPlaceholderOrigin"/> 补偿，避免预览溢出面板。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectOverlayPresenter : MonoBehaviour
    {
        private const string InspectLiveFaceName = "__InspectLiveFace";
        /// <summary>与半黑屏 / 右键面板同层。须高于 BounceFan 选项卡（BaseSortingOrder≈6，悬停可到 ~49）。</summary>
        private const string InspectSortingLayerName = "UI";
        /// <summary>右键面板根 SortingGroup：整棵子树作为一组压过三选一选项卡。</summary>
        private const int InspectOverlaySortingOrder = 100;
        /// <summary>嵌套于面板根 SG 内：高于面板底图 / 文案，低于关闭钮等若另挂更高序。</summary>
        private const int InspectFaceSortingOrder = 5;

        private static CardInspectOverlayPresenter s_instance;

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject enemyPanel;
        [SerializeField] private GameObject regularPanel;
        [SerializeField] private Transform enemyCardFace;
        [SerializeField] private Transform regularCardFace;
        [SerializeField] private TMP_Text enemyFaceIntro;
        [SerializeField] private TMP_Text enemyDeckIntro;
        [SerializeField] private TMP_Text regularFaceIntro;
        [SerializeField] private TMP_Text regularDeckIntro;
        [SerializeField] private CardInspectGlossaryListView enemyGlossaryList;
        [SerializeField] private CardInspectGlossaryListView regularGlossaryList;

        private bool _open;
        private bool _dimmerHeld;
        private CardFacePresentationBinder _enemyBinder;
        private CardFacePresentationBinder _regularBinder;
        private CardPresentationKind _enemyLiveKind = CardPresentationKind.Unknown;
        private CardPresentationKind _regularLiveKind = CardPresentationKind.Unknown;
        private CardInspectGlossaryRowView _rowPrefab;

        public static bool IsOpen
        {
            get
            {
                if (!TryGetLiveInstance(out var live))
                {
                    return false;
                }

                return live._open;
            }
        }

        public static CardInspectOverlayPresenter InstanceOrNull()
        {
            return TryGetLiveInstance(out var live) ? live : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (TryGetLiveInstance(out _))
            {
                return;
            }

            var existing = FindFirstObjectByType<CardInspectOverlayPresenter>();
            if (existing != null)
            {
                s_instance = existing;
                return;
            }

            var uiRoot = FindSceneObject("UI面板");
            if (uiRoot == null)
            {
                return;
            }

            var inspectRoot = uiRoot.transform.Find("右键描述");
            if (inspectRoot == null)
            {
                return;
            }

            var presenter = inspectRoot.gameObject.GetComponent<CardInspectOverlayPresenter>();
            if (presenter == null)
            {
                presenter = inspectRoot.gameObject.AddComponent<CardInspectOverlayPresenter>();
            }

            presenter.BindScene(uiRoot, inspectRoot.gameObject);
            presenter.HideAllImmediate();
        }

        public void BindScene(GameObject uiPanelRoot, GameObject inspectRoot)
        {
            root = inspectRoot;
            BattleUiDimmerOverlay.EnsureBound(
                uiPanelRoot != null ? uiPanelRoot.transform.Find("半黑屏BG")?.gameObject : null);

            enemyPanel = FindChild(inspectRoot.transform, "敌方描述专用BG")?.gameObject;
            regularPanel = FindChild(inspectRoot.transform, "常规描述BG")?.gameObject;

            enemyCardFace = FindChild(enemyPanel != null ? enemyPanel.transform : null, "敌人卡模板占位");
            regularCardFace = FindChild(regularPanel != null ? regularPanel.transform : null, "道具卡标准模版");

            WirePanelSlots(
                enemyPanel,
                out enemyFaceIntro,
                out enemyDeckIntro,
                out var enemyClose,
                out enemyGlossaryList);
            WirePanelSlots(
                regularPanel,
                out regularFaceIntro,
                out regularDeckIntro,
                out var regularClose,
                out regularGlossaryList);

            WireCloseButton(enemyClose);
            WireCloseButton(regularClose);
            WirePanelDismiss(enemyPanel);
            WirePanelDismiss(regularPanel);

            // 占位只作锚点：关掉场景里放的成品 mock 图，真卡面开面板时再挂。
            HidePlaceholderMockVisuals(enemyCardFace);
            HidePlaceholderMockVisuals(regularCardFace);
            _enemyBinder = null;
            _regularBinder = null;
            _enemyLiveKind = CardPresentationKind.Unknown;
            _regularLiveKind = CardPresentationKind.Unknown;
            EnsureGlossaryListsConfigured();
        }

        public static bool TryOpen(ManagedCard card)
        {
            EnsureExists();
            if (!TryGetLiveInstance(out var live) || card == null)
            {
                return false;
            }

            return live.Open(card);
        }

        /// <summary>
        /// 无 ManagedCard 时按 defId 开详述（装备栏遗物等，ADR-0027）。
        /// </summary>
        public static bool TryOpenByDefId(string defId, CardPresentationKind kindHint = CardPresentationKind.Unknown)
        {
            EnsureExists();
            if (!TryGetLiveInstance(out var live) || string.IsNullOrEmpty(defId))
            {
                return false;
            }

            return live.OpenByDefId(defId, kindHint);
        }

        public static void CloseIfOpen()
        {
            if (TryGetLiveInstance(out var live))
            {
                live.Close();
            }
        }

        public bool Open(ManagedCard card)
        {
            if (card == null)
            {
                return false;
            }

            // ChoiceOverlay 下主路径仍由 PointerHitRouter 禁开；BounceFan 合法悬停可主动 TryOpen。

            if (root == null)
            {
                EnsureExists();
            }

            var isMonster = card.CoreKind == CardPresentationKind.Monster;
            var snapshot = CloneForInspect(card.CommittedPresentation, card);
            // ADR-0035：检查永远静态检查描述（初始装配实参），不展示局内模板/已提交剩余。
            snapshot.BasicDescription = ResolveInspectBasicDescription(card.DefId, snapshot.BasicDescription);
            var arch = NineGridArchitecture.Interface;
            var catalog = arch?.GetSystem<IContentSystem>()?.Catalog;
            var texts = CardInspectDetailComposer.Compose(card.DefId, snapshot, catalog);
            return PresentInspect(isMonster, card, snapshot, texts);
        }

        public bool OpenByDefId(string defId, CardPresentationKind kindHint = CardPresentationKind.Unknown)
        {
            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            if (root == null)
            {
                EnsureExists();
            }

            var kind = kindHint != CardPresentationKind.Unknown
                ? kindHint
                : CoreCardPresentationMapper.ResolvePresentationKindFromDefId(defId);
            var snapshot = CoreCardPresentationMapper.BuildVisualSnapshotFromDefId(defId, kind);
            // ADR-0035：检查面板永不展示局内模板/已提交剩余，重新按检查模式投影。
            snapshot.BasicDescription = ResolveInspectBasicDescription(defId, snapshot.BasicDescription);
            var arch = NineGridArchitecture.Interface;
            var catalog = arch?.GetSystem<IContentSystem>()?.Catalog;
            var texts = CardInspectDetailComposer.Compose(defId, snapshot, catalog);
            return PresentInspect(isMonster: false, card: null, snapshot, texts, kindOverride: kind);
        }

        private bool PresentInspect(
            bool isMonster,
            ManagedCard card,
            CardPresentationSnapshot snapshot,
            CardInspectDetailComposer.Result texts,
            CardPresentationKind kindOverride = CardPresentationKind.Unknown)
        {
            if (!_open)
            {
                if (!BattleUiDimmerOverlay.TryAcquire("card-inspect"))
                {
                    return false;
                }

                _dimmerHeld = true;
            }

            _open = true;
            if (root != null && !root.activeSelf)
            {
                root.SetActive(true);
            }

            // BounceFan 选项在 UI 层 order 6+；面板底图多为 0–3。根 SG 提到 100 才能整组压住三选一。
            EnsureOverlaySortingGroup();
            EnsureGlossaryListsConfigured();

            SetActiveSafe(enemyPanel, isMonster);
            SetActiveSafe(regularPanel, !isMonster);

            var glossaryCatalog = CardFacePresentationBinder.PeekDescriptionIconCatalog();
            var terms = CardGlossaryTerms.ExtractExplicitTerms(
                snapshot != null ? snapshot.BasicDescription : null,
                glossaryCatalog);

            if (isMonster)
            {
                _enemyBinder = EnsureLiveFace(enemyCardFace, card, ref _enemyLiveKind);
                ApplyFace(_enemyBinder, snapshot);
                if (_enemyBinder != null)
                {
                    ApplyInspectSorting(_enemyBinder.gameObject);
                }

                SetText(enemyFaceIntro, texts.FaceIntro);
                SetText(enemyDeckIntro, texts.DeckIntro);
                BindGlossaryPanel(enemyGlossaryList, terms, _enemyBinder, glossaryCatalog, snapshot);
            }
            else
            {
                _regularBinder = card != null
                    ? EnsureLiveFace(regularCardFace, card, ref _regularLiveKind)
                    : EnsureLiveFaceByKind(regularCardFace, kindOverride, ref _regularLiveKind);
                ApplyFace(_regularBinder, snapshot);
                if (_regularBinder != null)
                {
                    ApplyInspectSorting(_regularBinder.gameObject);
                }

                SetText(regularFaceIntro, texts.FaceIntro);
                SetText(regularDeckIntro, texts.DeckIntro);
                BindGlossaryPanel(regularGlossaryList, terms, _regularBinder, glossaryCatalog, snapshot);
            }

            return true;
        }

        public void Close()
        {
            if (!_open)
            {
                HideAllImmediate();
                return;
            }

            _open = false;
            HideAllImmediate();
            if (_dimmerHeld)
            {
                BattleUiDimmerOverlay.Release("card-inspect");
                _dimmerHeld = false;
            }
        }

        private void Awake()
        {
            s_instance = this;
            if (root == null)
            {
                root = gameObject;
            }

            var uiRoot = transform.root != null && transform.root.name == "UI面板"
                ? transform.root.gameObject
                : FindSceneObject("UI面板");
            if (enemyPanel == null || regularPanel == null)
            {
                BindScene(uiRoot, root);
            }

            HideAllImmediate();
        }

        private void OnDestroy()
        {
            if (_dimmerHeld)
            {
                BattleUiDimmerOverlay.Release("card-inspect");
                _dimmerHeld = false;
            }

            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Unity 已销毁对象对 C# <c>?</c> 仍非 null；须走重载 <c>==</c> 并清掉静态残留。
        /// </summary>
        private static bool TryGetLiveInstance(out CardInspectOverlayPresenter live)
        {
            if (s_instance == null)
            {
                s_instance = null;
                live = null;
                return false;
            }

            live = s_instance;
            return true;
        }

        private void EnsureGlossaryListsConfigured()
        {
            if (_rowPrefab == null)
            {
                var go = CardChassisPaths.LoadGameObject(CardChassisPaths.GlossaryRowPrefab);
                if (go != null)
                {
                    _rowPrefab = go.GetComponent<CardInspectGlossaryRowView>();
                }
            }

            ConfigureGlossaryList(ref enemyGlossaryList, enemyPanel);
            ConfigureGlossaryList(ref regularGlossaryList, regularPanel);
        }

        private void ConfigureGlossaryList(ref CardInspectGlossaryListView list, GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            var content = FindScrollContent(panel.transform);
            if (content == null)
            {
                return;
            }

            if (list == null)
            {
                list = content.GetComponent<CardInspectGlossaryListView>();
                if (list == null)
                {
                    list = content.gameObject.AddComponent<CardInspectGlossaryListView>();
                }
            }

            list.Configure(content, _rowPrefab);
        }

        private static RectTransform FindScrollContent(Transform panelRoot)
        {
            if (panelRoot == null)
            {
                return null;
            }

            var scroll = panelRoot.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            if (scroll != null && scroll.content != null)
            {
                return scroll.content;
            }

            var content = FindChild(panelRoot, "Content");
            return content as RectTransform;
        }

        private void BindGlossaryPanel(
            CardInspectGlossaryListView list,
            IReadOnlyList<CardGlossaryTerms.ResolvedTerm> terms,
            CardFacePresentationBinder binder,
            CardFaceDescriptionIconCatalogSO glossaryCatalog,
            CardPresentationSnapshot snapshot)
        {
            if (list != null)
            {
                list.BindExplicitTerms(terms);
            }

            WireIconHover(binder, list, glossaryCatalog, snapshot);
        }

        /// <summary>
        /// 预览卡面挂图标 hover 探针：描述内联 <c>[code]</c> 与卡面机制图标共用词条栏首行解释槽。
        /// 描述槽可缺（遗物等模板），此时只解释卡面图标。
        /// </summary>
        private static void WireIconHover(
            CardFacePresentationBinder binder,
            CardInspectGlossaryListView list,
            CardFaceDescriptionIconCatalogSO catalog,
            CardPresentationSnapshot snapshot)
        {
            if (binder == null)
            {
                return;
            }

            CardFaceSlotNodeMap.TryFindText(
                binder.transform,
                CardFaceSlotCodes.BasicDescription,
                out var description);

            var hover = binder.GetComponent<CardInspectIconHover>();
            if (hover == null)
            {
                hover = binder.gameObject.AddComponent<CardInspectIconHover>();
            }

            hover.Configure(description, list, catalog, Camera.main, snapshot);
        }

        private void HideAllImmediate()
        {
            if (enemyGlossaryList != null)
            {
                enemyGlossaryList.ClearAll();
            }

            if (regularGlossaryList != null)
            {
                regularGlossaryList.ClearAll();
            }

            SetActiveSafe(enemyPanel, false);
            SetActiveSafe(regularPanel, false);
            if (root != null && root.activeSelf)
            {
                // 保留根节点便于再次打开；仅关两态 BG。
            }
        }

        private static void ApplyFace(CardFacePresentationBinder binder, CardPresentationSnapshot snapshot)
        {
            if (binder == null || snapshot == null)
            {
                return;
            }

            binder.ApplyPresentation(snapshot);
        }

        /// <summary>
        /// 关掉锚点下场景摆的成品 mock（占位图），保留/准备 <see cref="InspectLiveFaceName"/> 真卡面。
        /// </summary>
        private static void HidePlaceholderMockVisuals(Transform anchor)
        {
            if (anchor == null)
            {
                return;
            }

            // 锚点自身若挂了成品整卡 Sprite，一并关掉。
            var selfRenderers = anchor.GetComponents<Renderer>();
            for (var i = 0; i < selfRenderers.Length; i++)
            {
                if (selfRenderers[i] != null)
                {
                    selfRenderers[i].enabled = false;
                }
            }

            for (var i = 0; i < anchor.childCount; i++)
            {
                var child = anchor.GetChild(i);
                if (child == null || child.name == InspectLiveFaceName)
                {
                    continue;
                }

                if (child.gameObject.activeSelf)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private CardFacePresentationBinder EnsureLiveFace(
            Transform anchor,
            ManagedCard card,
            ref CardPresentationKind liveKind)
        {
            if (anchor == null || card == null)
            {
                return null;
            }

            HidePlaceholderMockVisuals(anchor);

            var kind = card.CoreKind;
            if (kind == CardPresentationKind.Unknown)
            {
                kind = CardPresentationKindResolver.FromDefId(card.DefId);
            }

            return EnsureLiveFaceCore(anchor, card, kind, ref liveKind);
        }

        private CardFacePresentationBinder EnsureLiveFaceByKind(
            Transform anchor,
            CardPresentationKind kind,
            ref CardPresentationKind liveKind)
        {
            if (anchor == null || kind == CardPresentationKind.Unknown)
            {
                return null;
            }

            HidePlaceholderMockVisuals(anchor);
            return EnsureLiveFaceCore(anchor, card: null, kind, ref liveKind);
        }

        private CardFacePresentationBinder EnsureLiveFaceCore(
            Transform anchor,
            ManagedCard card,
            CardPresentationKind kind,
            ref CardPresentationKind liveKind)
        {
            var existing = anchor.Find(InspectLiveFaceName);
            if (existing != null && liveKind == kind)
            {
                var reuse = existing.GetComponent<CardFacePresentationBinder>();
                if (reuse != null)
                {
                    existing.gameObject.SetActive(true);
                    return reuse;
                }
            }

            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            var faceGo = CreateLiveFaceObject(anchor, card, kind);
            if (faceGo == null)
            {
                liveKind = CardPresentationKind.Unknown;
                return null;
            }

            liveKind = kind;
            var binder = faceGo.GetComponent<CardFacePresentationBinder>();
            if (binder == null)
            {
                binder = faceGo.AddComponent<CardFacePresentationBinder>();
            }

            return binder;
        }

        private static GameObject CreateLiveFaceObject(
            Transform anchor,
            ManagedCard card,
            CardPresentationKind kind)
        {
            GameObject source = null;

            // 优先克隆场上该卡已挂好的真卡面（与局内一致）。
            if (card != null && card.MountedFaceRoot != null)
            {
                source = card.MountedFaceRoot.gameObject;
            }

            if (source == null)
            {
                source = LoadFacePrefab(kind);
            }

            if (source == null)
            {
                return null;
            }

            var face = Instantiate(source, anchor);
            face.name = InspectLiveFaceName;
            face.transform.localPosition = Vector3.zero;
            face.transform.localRotation = Quaternion.identity;
            face.transform.localScale = Vector3.one;
            face.SetActive(true);

            // 常规面板占位按道具卡（卡框在本地原点）布局；遗物卡面内容整体偏在 x≈-1.22。
            AlignLiveFaceToPlaceholderOrigin(face.transform);

            // 真卡面预制体默认在 Main 层，会沉到 UI 面板后面；挂 SG 提到 UI 层。
            ApplyInspectSorting(face);
            return face;
        }

        /// <summary>
        /// 将真卡面平移，使「卡框 / 主边框」落到占位本地原点。
        /// 道具/机关等卡框已在原点时为 no-op；遗物卡标准模版卡框在 (-1.22, …) 时补偿偏左溢出。
        /// </summary>
        public static void AlignLiveFaceToPlaceholderOrigin(Transform face)
        {
            if (face == null)
            {
                return;
            }

            if (!TryGetInspectFrameLocalCenter(face, out var frameLocal))
            {
                face.localPosition = Vector3.zero;
                return;
            }

            // face 在占位下 scale=1、旋转恒等：卡框相对占位 = face.localPosition + frameLocal。
            face.localPosition = -frameLocal;
        }

        private static bool TryGetInspectFrameLocalCenter(Transform face, out Vector3 localCenter)
        {
            localCenter = Vector3.zero;
            if (face == null)
            {
                return false;
            }

            var frame = FindChild(face, "卡框") ?? FindChild(face, "Card_Border_rectangle_bronze");
            if (frame == null)
            {
                return false;
            }

            if (frame.parent == face)
            {
                localCenter = frame.localPosition;
                return true;
            }

            // 嵌套时把世界点折回 face 本地（创建瞬间父子 scale 均为 1）。
            localCenter = face.InverseTransformPoint(frame.position);
            return true;
        }

        private void EnsureOverlaySortingGroup()
        {
            var host = root != null ? root : gameObject;
            if (host == null)
            {
                return;
            }

            var sortingGroup = host.GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = host.AddComponent<SortingGroup>();
            }

            sortingGroup.sortingLayerName = InspectSortingLayerName;
            sortingGroup.sortingOrder = InspectOverlaySortingOrder;
        }

        private static void ApplyInspectSorting(GameObject face)
        {
            if (face == null)
            {
                return;
            }

            var sortingGroup = face.GetComponent<SortingGroup>();
            if (sortingGroup == null)
            {
                sortingGroup = face.AddComponent<SortingGroup>();
            }

            // 嵌套于面板根 SG：相对序只在覆层内比较；绝对压过 BounceFan 靠根 SG order=100。
            sortingGroup.sortingLayerName = InspectSortingLayerName;
            sortingGroup.sortingOrder = InspectFaceSortingOrder;
            CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(sortingGroup);
            CardMainVisualMaskAnchor.ResyncAllVisibleInsideMasks(face.transform);
        }

        private static GameObject LoadFacePrefab(CardPresentationKind kind)
        {
            string path;
            switch (kind)
            {
                case CardPresentationKind.Avatar:
                    path = CardChassisPaths.AvatarFacePrefab;
                    break;
                case CardPresentationKind.Monster:
                    path = CardChassisPaths.MonsterFacePrefab;
                    break;
                case CardPresentationKind.Trap:
                    path = CardChassisPaths.TrapFacePrefab;
                    break;
                case CardPresentationKind.HelpCard:
                case CardPresentationKind.Item:
                case CardPresentationKind.PlayerCard:
                    path = CardChassisPaths.ItemFacePrefab;
                    break;
                case CardPresentationKind.Relic:
                    path = CardChassisPaths.RelicFacePrefab;
                    break;
                default:
                    return null;
            }

            return CardChassisPaths.LoadGameObject(path);
        }

        /// <summary>
        /// ADR-0035 / #155：检查面板描述恒为静态检查描述（检查模式投影，初始装配实参插值）。
        /// 有 JSON 时按 <c>description</c> 重投影；无 JSON（如 QuickTest 动态卡）回退原值。
        /// </summary>
        private static string ResolveInspectBasicDescription(string defId, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(defId)
                && CardPresentationConfigCatalog.TryGet(defId.Trim(), out var dto)
                && dto != null)
            {
                return CardFaceDescriptionProjector.Project(
                    CardDescriptionProjectionMode.Inspect,
                    dto.description,
                    dto.effectAssemblies);
            }

            return fallback ?? string.Empty;
        }

        private static CardPresentationSnapshot CloneForInspect(
            CardPresentationSnapshot source,
            ManagedCard card)
        {
            if (source != null)
            {
                return new CardPresentationSnapshot
                {
                    Kind = source.Kind,
                    DefId = source.DefId,
                    DisplayName = source.DisplayName,
                    MainIcon = source.MainIcon,
                    FaceBackground = source.FaceBackground,
                    BackBorder = source.BackBorder,
                    BackShirt = source.BackShirt,
                    BackLogo = source.BackLogo,
                    CardFrame = source.CardFrame,
                    Banner = source.Banner,
                    Attack = source.Attack,
                    Armor = source.Armor,
                    Hp = source.Hp,
                    ActionCount = source.ActionCount,
                    AttackPattern = source.AttackPattern,
                    HasSyncRhythmSkills = source.HasSyncRhythmSkills,
                    HasActiveRhythm = source.HasActiveRhythm,
                    ShowActionCount = source.ShowActionCount,
                    FaceUp = true,
                    BasicDescription = source.BasicDescription,
                    DetailDescription = source.DetailDescription,
                    FaceIntro = source.FaceIntro,
                    FrameColor = source.FrameColor,
                    Rarity = source.Rarity,
                    CommittedCountdownRemaining = source.CommittedCountdownRemaining,
                };
            }

            return new CardPresentationSnapshot
            {
                Kind = card != null ? card.CoreKind : CardPresentationKind.Unknown,
                DefId = card != null ? card.DefId : string.Empty,
                FaceUp = true,
            };
        }

        private static void WirePanelSlots(
            GameObject panel,
            out TMP_Text faceIntro,
            out TMP_Text deckIntro,
            out Transform closeButton,
            out CardInspectGlossaryListView glossaryList)
        {
            faceIntro = null;
            deckIntro = null;
            closeButton = null;
            glossaryList = null;
            if (panel == null)
            {
                return;
            }

            var t = panel.transform;
            closeButton = FindChild(t, "关闭面板 (1)") ?? FindChild(t, "关闭面板");
            faceIntro = FindTmp(t, "背景介绍");
            deckIntro = FindTmp(t, "牌组介绍");

            var content = FindScrollContent(t);
            if (content != null)
            {
                // 旧单条 TMP 模板：隐藏，改由动态行预制体填充。
                for (var i = content.childCount - 1; i >= 0; i--)
                {
                    var child = content.GetChild(i);
                    if (child != null
                        && (child.name.StartsWith("详细效果信息", System.StringComparison.Ordinal)
                            || child.name.Contains("详细效果信息")))
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                glossaryList = content.GetComponent<CardInspectGlossaryListView>();
                if (glossaryList == null)
                {
                    glossaryList = content.gameObject.AddComponent<CardInspectGlossaryListView>();
                }
            }
        }

        private static void WireCloseButton(Transform close)
        {
            if (close == null)
            {
                return;
            }

            var col = close.GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = close.gameObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.6f, 0.6f);
            }

            col.enabled = true;
            var proxy = close.GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = close.gameObject.AddComponent<UiOverlayHitProxy>();
            }

            proxy.Configure(
                UiOverlayHitAction.CloseCardInspect,
                BattleUiDimmerOverlay.CloseHitSort,
                PointerHitSurfacePriorities.Overlay);
        }

        /// <summary>
        /// 点在描述 BG 图范围内（非关闭钮等更高序控件）即可关面板。
        /// 半黑屏在面板外点击亦关（见 <see cref="BattleUiDimmerOverlay"/> Swallow 分支）。
        /// </summary>
        private static void WirePanelDismiss(GameObject panel)
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

            var renderer = panel.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                var size = renderer.sprite.bounds.size;
                col.size = new Vector2(size.x, size.y);
                col.offset = renderer.sprite.bounds.center;
            }
            else
            {
                col.size = new Vector2(8f, 10f);
                col.offset = Vector2.zero;
            }

            col.isTrigger = false;
            col.enabled = true;

            var proxy = panel.GetComponent<UiOverlayHitProxy>();
            if (proxy == null)
            {
                proxy = panel.AddComponent<UiOverlayHitProxy>();
            }

            // 覆层内：低于关闭钮 CloseHitSort，高于半黑屏 HitSort；表面优先级相同（Overlay）。
            proxy.Configure(
                UiOverlayHitAction.CloseCardInspect,
                BattleUiDimmerOverlay.HitSort + 1,
                PointerHitSurfacePriorities.Overlay);
        }

        private static TMP_Text FindTmp(Transform root, string name)
        {
            var node = FindChild(root, name);
            return node != null ? node.GetComponent<TMP_Text>() : null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static GameObject FindSceneObject(string name)
        {
            var found = GameObject.Find(name);
            if (found != null)
            {
                return found;
            }

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i];
                }
            }

            return null;
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private static void SetText(TMP_Text tmp, string value)
        {
            if (tmp != null)
            {
                tmp.text = value ?? string.Empty;
            }
        }
    }
}
