using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation;
using TMPro;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 右键卡牌详述面板：敌方 / 常规两态，半黑屏挡交互，不暂停主线。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardInspectOverlayPresenter : MonoBehaviour
    {
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

        public static bool IsOpen => s_instance != null && s_instance._open;

        public static CardInspectOverlayPresenter InstanceOrNull() => s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<CardInspectOverlayPresenter>() != null)
            {
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

            _enemyBinder = EnsureFaceBinder(enemyCardFace);
            _regularBinder = EnsureFaceBinder(regularCardFace);
        }

        public static bool TryOpen(ManagedCard card)
        {
            EnsureExists();
            if (s_instance == null || card == null)
            {
                return false;
            }

            return s_instance.Open(card);
        }

        public static void CloseIfOpen()
        {
            s_instance?.Close();
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
                ApplyFace(_enemyBinder, snapshot);
                SetText(enemyFaceIntro, texts.FaceIntro);
                SetText(enemyDeckIntro, texts.DeckIntro);
                SetText(enemySkillDetails, texts.SkillDetails);
            }
            else
            {
                ApplyFace(_regularBinder, snapshot);
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

            if (ReferenceEquals(s_instance, this))
            {
                s_instance = null;
            }
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

        private static CardFacePresentationBinder EnsureFaceBinder(Transform faceRoot)
        {
            if (faceRoot == null)
            {
                return null;
            }

            var binder = faceRoot.GetComponent<CardFacePresentationBinder>();
            if (binder == null)
            {
                binder = faceRoot.gameObject.AddComponent<CardFacePresentationBinder>();
            }

            return binder;
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
