using NineGrid.Cards;
using NineGrid.Cards.Anim;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Flow
{
    /// <summary>
    /// 右键卡牌详述面板：敌方 / 常规两态，半黑屏挡交互，不暂停主线。
    /// 场景里的「敌人卡模板占位」「道具卡标准模版」只作锚点；成品 mock 图会被关掉，
    /// 运行时在锚点下挂真卡面预制体并 Commit。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectOverlayPresenter : MonoBehaviour
    {
        private const string InspectLiveFaceName = "__InspectLiveFace";
        /// <summary>与半黑屏 / 右键面板同层，高于面板底图（order 0）与槽位（1）。</summary>
        private const string InspectSortingLayerName = "UI";
        private const int InspectFaceSortingOrder = 5;

        private static CardInspectOverlayPresenter s_instance;

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject enemyPanel;
        [SerializeField] private GameObject regularPanel;
        [SerializeField] private Transform enemyCardFace;
        [SerializeField] private Transform regularCardFace;
        [SerializeField] private TMP_Text enemyFaceIntro;
        [SerializeField] private TMP_Text enemyDeckIntro;
        [SerializeField] private TMP_Text enemySkillDetails;
        [SerializeField] private TMP_Text regularFaceIntro;
        [SerializeField] private TMP_Text regularDeckIntro;
        [SerializeField] private TMP_Text regularSkillDetails;

        private bool _open;
        private bool _dimmerHeld;
        private CardFacePresentationBinder _enemyBinder;
        private CardFacePresentationBinder _regularBinder;
        private CardPresentationKind _enemyLiveKind = CardPresentationKind.Unknown;
        private CardPresentationKind _regularLiveKind = CardPresentationKind.Unknown;

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
                out enemySkillDetails,
                out var enemyClose);
            WirePanelSlots(
                regularPanel,
                out regularFaceIntro,
                out regularDeckIntro,
                out regularSkillDetails,
                out var regularClose);

            WireCloseButton(enemyClose);
            WireCloseButton(regularClose);

            // 占位只作锚点：关掉场景里放的成品 mock 图，真卡面开面板时再挂。
            HidePlaceholderMockVisuals(enemyCardFace);
            HidePlaceholderMockVisuals(regularCardFace);
            _enemyBinder = null;
            _regularBinder = null;
            _enemyLiveKind = CardPresentationKind.Unknown;
            _regularLiveKind = CardPresentationKind.Unknown;
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

            // 选择叠层已占用输入时不抢右键详述。
            if (PresentationInputGates.ChoiceOverlayActive && !_open)
            {
                return false;
            }

            if (root == null)
            {
                EnsureExists();
            }

            var isMonster = card.CoreKind == CardPresentationKind.Monster;
            var snapshot = CloneForInspect(card.CommittedPresentation, card);
            var catalog = NineGridArchitecture.Interface?.GetSystem<IContentSystem>()?.Catalog;
            var texts = CardInspectDetailComposer.Compose(card.DefId, snapshot, catalog);

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

            SetActiveSafe(enemyPanel, isMonster);
            SetActiveSafe(regularPanel, !isMonster);

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
                SetText(enemySkillDetails, texts.SkillDetails);
            }
            else
            {
                _regularBinder = EnsureLiveFace(regularCardFace, card, ref _regularLiveKind);
                ApplyFace(_regularBinder, snapshot);
                if (_regularBinder != null)
                {
                    ApplyInspectSorting(_regularBinder.gameObject);
                }

                SetText(regularFaceIntro, texts.FaceIntro);
                SetText(regularDeckIntro, texts.DeckIntro);
                SetText(regularSkillDetails, texts.SkillDetails);
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

        private void HideAllImmediate()
        {
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
            if (card.MountedFaceRoot != null)
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

            // 真卡面预制体默认在 Main 层，会沉到 UI 面板后面；挂 SG 提到 UI 层。
            ApplyInspectSorting(face);
            return face;
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

            sortingGroup.sortingLayerName = InspectSortingLayerName;
            sortingGroup.sortingOrder = InspectFaceSortingOrder;
            CardMainVisualMaskAnchor.PropagateSortingLayerFromGroup(sortingGroup);
            CardMainVisualMaskAnchor.ResyncAllVisibleInsideMasks(face.transform);
        }

        private static GameObject LoadFacePrefab(CardPresentationKind kind)
        {
#if UNITY_EDITOR
            string path;
            switch (kind)
            {
                case CardPresentationKind.Avatar:
                    path = CardChassisPaths.AvatarFacePrefab;
                    break;
                case CardPresentationKind.Monster:
                    path = CardChassisPaths.MonsterFacePrefab;
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

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
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
                    FaceUp = true,
                    BasicDescription = source.BasicDescription,
                    DetailDescription = source.DetailDescription,
                    FaceIntro = source.FaceIntro,
                    FrameColor = source.FrameColor,
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
            out TMP_Text skillDetails,
            out Transform closeButton)
        {
            faceIntro = null;
            deckIntro = null;
            skillDetails = null;
            closeButton = null;
            if (panel == null)
            {
                return;
            }

            var t = panel.transform;
            closeButton = FindChild(t, "关闭面板 (1)") ?? FindChild(t, "关闭面板");
            faceIntro = FindTmp(t, "背景介绍");
            deckIntro = FindTmp(t, "牌组介绍");
            skillDetails = FindTmp(t, "详细效果信息");
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

            proxy.Configure(UiOverlayHitAction.CloseCardInspect, BattleUiDimmerOverlay.CloseHitSort, 110);
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
