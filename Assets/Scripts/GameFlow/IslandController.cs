using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using NineGrid.Data;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 岛屿场景（IslandScene）：精炼厂 / 船坞面板。
    /// 精炼厂：买矿石、删矿、强化矿、刷新。
    /// 船坞：船体改造、铸造台强化、附魔、刷新。
    /// 离开 → 推进主流程。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandController : MonoBehaviour
    {
        [System.Serializable]
        sealed class ServicePanel
        {
            [Tooltip("触发该面板的场景可悬停对象。")]
            public SelectableSceneElement trigger;
            [Tooltip("面板根物体。")]
            public Transform panel;
            [Tooltip("入场点位（收起位置，通常在左侧屏幕外）。留空则用展开位置左移 offscreenLeftOffset。")]
            public Transform entryPoint;
            [Tooltip("展开点位（面板停留处）。留空则用面板初始位置。")]
            public Transform shownPoint;
            [Tooltip("悬停时的提示文案。")]
            [TextArea(1, 3)]
            public string hoverNotice;

            [System.NonSerialized] public Vector3 ShownPos;
            [System.NonSerialized] public Vector3 EntryPos;
            [System.NonSerialized] public bool Open;
            [System.NonSerialized] public bool Hovering;
        }

        sealed class PanelTarget
        {
            public Collider2D Collider;
            public Func<string> GetHoverText;
            public Action OnClick;
        }

        [Header("服务面板（精炼厂 / 船坞）")]
        [SerializeField] ServicePanel refinery = new ServicePanel { hoverNotice = "精炼厂：买矿石、精炼服务。" };
        [SerializeField] ServicePanel shipyard = new ServicePanel { hoverNotice = "船坞：船体改造与修整。" };

        [Header("离开")]
        [Tooltip("留空时按名字「离开」/「返回地图」查找。")]
        [SerializeField] SelectableSceneElement leaveElement;

        [Header("引用 / 参数")]
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] Camera worldCamera;
        [Tooltip("面板入场点位（收起处，通常屏幕左外），两个面板共用；留空时按名字「panel 入场点位」查找。")]
        [SerializeField] Transform sharedEntryPoint;
        [SerializeField] float slideDuration = 0.4f;
        [SerializeField] Ease slideInEase = Ease.OutCubic;
        [SerializeField] Ease slideOutEase = Ease.InCubic;
        [Tooltip("入场点位未指定时，展开位置向左偏移量（世界单位）。")]
        [SerializeField] float offscreenLeftOffset = 12f;

        [Header("矿石库 / 金币")]
        [SerializeField] OreInventoryPanel oreInventoryPanel;

        ShopController _shop;
        GameObject _shopDescriptionObject;
        GameObject _goldCountObject;
        TMP_Text _shopDescriptionText;
        TMP_Text _goldCountText;
        TextAnimator_TMP _shopDescriptionAnimator;
        TextAnimator_TMP _goldCountAnimator;
        Transform _refineryGoldIcon;
        Transform _shipyardGoldIcon;
        bool _shopHudVisible;
        readonly List<PanelTarget> _refineryPanelTargets = new List<PanelTarget>(8);
        readonly List<PanelTarget> _shipyardPanelTargets = new List<PanelTarget>(4);
        readonly List<(Transform slot, SpriteRenderer renderer)> _refineryOreSlots = new List<(Transform, SpriteRenderer)>(5);
        readonly List<SpriteRenderer> _shipyardRelicSlots = new List<SpriteRenderer>(3);
        PanelTarget _hoveredPanelTarget;
        int _hoveredOreDeckIndex = -1;
        bool _shopInitialized;
        bool _panelBindingsReady;
        bool _refineryBindingsReady;
        bool _shipyardBindingsReady;
        bool _left;
        Coroutine _prepareRoutine;

        public event Action RefineryOpened;
        public event Action ShipyardOpened;
        public event Action PanelSwitched;
        public event Action Left;
        /// <summary>岛屿场景就绪（面板初始化完成）。</summary>
        public event Action VisitPrepared;

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SchedulePrepareVisit();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_prepareRoutine != null)
            {
                StopCoroutine(_prepareRoutine);
                _prepareRoutine = null;
            }
        }

        void Start()
        {
            SchedulePrepareVisit();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, GameFlowScenes.Island, StringComparison.Ordinal))
            {
                return;
            }

            SchedulePrepareVisit();
        }

        void SchedulePrepareVisit()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_prepareRoutine != null)
            {
                StopCoroutine(_prepareRoutine);
            }

            _prepareRoutine = StartCoroutine(PrepareVisitRoutine());
        }

        IEnumerator PrepareVisitRoutine()
        {
            _left = false;
            yield return null;
            yield return null;

            _prepareRoutine = null;
            PrepareVisit();
        }

        void PrepareVisit()
        {
            _left = false;
            ResolveRefs(forceRefresh: true);
            InitPanel(refinery);
            InitPanel(shipyard);
            InitShop();
            _panelBindingsReady = false;
            _refineryBindingsReady = false;
            _shipyardBindingsReady = false;
            BindPanelClickTargets();
            VisitPrepared?.Invoke();
        }

        void InitShop()
        {
            if (_shopInitialized) return;
            _shopInitialized = true;

            var run = RunData.Ensure();
            var oreCatalog = GameDataCatalogs.Ore;
            var hullModCatalog = GameDataCatalogs.HullMod;
            _shop = new ShopController(run, oreCatalog, hullModCatalog);

            if (run.ShopOres.Count == 0)
            {
                _shop.GenerateRefineryStock();
            }

            if (run.ShopRelics.Count == 0 || run.EnchantOptions.Count == 0)
            {
                _shop.GenerateShipyardStock();
            }

            if (run.ShopOres.Count == 0)
            {
                Debug.LogWarning("[Island] 精炼厂库存为空，请确认 OreCatalog 已配置且含矿石条目。");
            }
        }

        void Update()
        {
            if (_left)
            {
                return;
            }

            if (pointerSelector == null)
            {
                ResolveRefs();
                if (pointerSelector == null)
                {
                    return;
                }
            }

            if (!_panelBindingsReady)
            {
                TryBindPanelClickTargets();
            }

            HandleShopInput();

            var clicked = FlowInput.PrimaryClickThisFrame();
            var hovered = pointerSelector.Hovered;
            var inOreSelection = oreInventoryPanel != null && oreInventoryPanel.IsSelectionMode;

            HandleHover(refinery, hovered);
            HandleHover(shipyard, hovered);

            if (clicked && TryHandleSceneNavigation(hovered))
            {
                return;
            }

            if (inOreSelection)
            {
                HandleOreInventorySelectionHover();
                return;
            }

            if (refinery.Open)
            {
                HandlePanelHover(_refineryPanelTargets);
                HandlePanelClicks(_refineryPanelTargets, refinery);
            }
            else if (shipyard.Open)
            {
                HandlePanelHover(_shipyardPanelTargets);
                HandlePanelClicks(_shipyardPanelTargets, shipyard);
            }
        }

        bool TryHandleSceneNavigation(SelectableSceneElement hovered)
        {
            if (leaveElement != null && hovered == leaveElement)
            {
                Leave();
                return true;
            }

            if (IsShipyardTrigger(hovered))
            {
                OpenShipyard();
                return true;
            }

            if (IsRefineryTrigger(hovered))
            {
                OpenRefinery();
                return true;
            }

            return false;
        }

        public void OpenRefinery()
        {
            RefineryOpened?.Invoke();
            PanelSwitched?.Invoke();
            SwitchTo(refinery, shipyard);
        }

        public void OpenShipyard()
        {
            ShipyardOpened?.Invoke();
            PanelSwitched?.Invoke();
            SwitchTo(shipyard, refinery);
        }

        public void Leave()
        {
            if (_left)
            {
                return;
            }

            _left = true;
            Left?.Invoke();
            HideNotice();

            oreInventoryPanel?.Close();

            HidePanelImmediate(refinery);
            HidePanelImmediate(shipyard);
            HideShopHud();
            TryReleaseShopOverlay();

            GameFlowController.Instance?.Advance();
        }

        void HidePanelImmediate(ServicePanel sp)
        {
            if (sp?.panel == null)
            {
                return;
            }

            sp.panel.DOKill();
            sp.Open = false;
            sp.panel.position = sp.EntryPos;
            sp.panel.gameObject.SetActive(false);
        }

        void SwitchTo(ServicePanel open, ServicePanel other)
        {
            if (other.Open)
            {
                SetPanelOpen(other, false);
            }

            SetPanelOpen(open, true);
        }

        void HandleHover(ServicePanel sp, SelectableSceneElement hovered)
        {
            if (sp.trigger == null || sp.Open)
            {
                if (sp.Hovering)
                {
                    sp.Hovering = false;
                    HideNotice();
                }

                return;
            }

            var isHover = sp == refinery
                ? IsRefineryTrigger(hovered)
                : IsShipyardTrigger(hovered);
            if (isHover == sp.Hovering)
            {
                return;
            }

            sp.Hovering = isHover;
            if (isHover && !string.IsNullOrEmpty(sp.hoverNotice))
            {
                ShowNotice(sp.hoverNotice);
            }
            else if (!isHover)
            {
                HideNotice();
            }
        }

        // 面板挂在常驻 UI（DontDestroyOnLoad）上，世界坐标会跨场景 / 跨次进岛保留；
        // 而 IslandController 每次进岛随场景重建。若把面板「当前位置」当展开位，
        // 上次离岛已把面板挪到入场点（屏幕外），二次进岛就会把展开位误存成屏外入场点，
        // 导致面板打开后仍停在屏外，表现为「点击没反应、面板打不开」。故用静态缓存记真实展开位。
        static Vector3? s_refineryShownPos;
        static Vector3? s_shipyardShownPos;

        void InitPanel(ServicePanel sp)
        {
            if (sp?.panel == null)
            {
                return;
            }

            sp.EntryPos = sp.entryPoint != null
                ? sp.entryPoint.position
                : sp.panel.position + Vector3.left * offscreenLeftOffset;

            sp.ShownPos = ResolveShownPos(sp);

            sp.panel.position = sp.EntryPos;
            sp.panel.gameObject.SetActive(false);
            sp.Open = false;
        }

        Vector3 ResolveShownPos(ServicePanel sp)
        {
            if (sp.shownPoint != null)
            {
                var explicitPos = sp.shownPoint.position;
                StoreShownPos(sp, explicitPos);
                return explicitPos;
            }

            var cached = GetCachedShownPos(sp);
            var current = sp.panel.position;
            // 面板已被移到入场点（屏幕外）时当前位置不可信，改用首次进岛缓存的真实展开位。
            var atEntry = (current - sp.EntryPos).sqrMagnitude < 1e-4f;
            if (cached.HasValue && atEntry)
            {
                return cached.Value;
            }

            if (atEntry)
            {
                // 无缓存且已在入场点：不应把屏外位置记成展开位。
                var fallback = sp.EntryPos + Vector3.right * offscreenLeftOffset;
                StoreShownPos(sp, fallback);
                return fallback;
            }

            StoreShownPos(sp, current);
            return current;
        }

        Vector3? GetCachedShownPos(ServicePanel sp)
        {
            if (sp == refinery) return s_refineryShownPos;
            if (sp == shipyard) return s_shipyardShownPos;
            return null;
        }

        void StoreShownPos(ServicePanel sp, Vector3 pos)
        {
            if (sp == refinery)
            {
                s_refineryShownPos = pos;
            }
            else if (sp == shipyard)
            {
                s_shipyardShownPos = pos;
            }
        }

        void SetPanelOpen(ServicePanel sp, bool open)
        {
            if (sp?.panel == null || sp.Open == open)
            {
                return;
            }

            sp.Open = open;
            sp.panel.DOKill();
            var target = open ? sp.ShownPos : sp.EntryPos;
            var ease = open ? slideInEase : slideOutEase;

            if (open)
            {
                sp.panel.position = sp.EntryPos;
                sp.panel.gameObject.SetActive(true);
                RefreshPanelPresentation(sp);
            }
            else
            {
                ClearPanelHover();
                HideShopDescription();
            }

            if (slideDuration <= 0f)
            {
                sp.panel.position = target;
                if (!open)
                {
                    sp.panel.gameObject.SetActive(false);
                }

                return;
            }

            var tween = sp.panel.DOMove(target, slideDuration).SetEase(ease).SetUpdate(true);
            if (!open)
            {
                var panelGo = sp.panel.gameObject;
                tween.OnComplete(() => panelGo.SetActive(false));
            }
        }

        void RefreshPanelPresentation(ServicePanel sp)
        {
            if (_shop == null)
            {
                return;
            }

            RefreshGoldDisplay();
            ShowShopHud();

            if (sp == refinery)
            {
                RefreshRefineryVisuals();
                ShowShopDescription(_shop.GetRefineryPanelHint());
            }
            else if (sp == shipyard)
            {
                RefreshShipyardVisuals();
                ShowShopDescription(_shop.GetShipyardPanelHint());
            }
        }

        void RefreshRefineryVisuals()
        {
            for (var i = 0; i < _refineryOreSlots.Count; i++)
            {
                var (slot, renderer) = _refineryOreSlots[i];
                if (renderer == null)
                {
                    continue;
                }

                if (i >= RunData.Ensure().ShopOres.Count)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                slot.gameObject.SetActive(true);
                var item = RunData.Ensure().ShopOres[i];
                var ore = GameDataCatalogs.Ore?.Get(item.OreId);
                if (ore?.Icon != null)
                {
                    renderer.sprite = ore.Icon;
                }

                renderer.color = item.Bought ? new Color(0.45f, 0.45f, 0.45f, 0.55f) : Color.white;
            }
        }

        void RefreshShipyardVisuals()
        {
            for (var i = 0; i < _shipyardRelicSlots.Count; i++)
            {
                var renderer = _shipyardRelicSlots[i];
                if (renderer == null)
                {
                    continue;
                }

                if (i >= RunData.Ensure().ShopRelics.Count)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                renderer.gameObject.SetActive(true);
                var item = RunData.Ensure().ShopRelics[i];
                renderer.color = item.Bought ? new Color(0.45f, 0.45f, 0.45f, 0.55f) : Color.white;
            }
        }

        void BindPanelClickTargets()
        {
            TryBindPanelClickTargets();
        }

        void TryBindPanelClickTargets()
        {
            if (_panelBindingsReady || _shop == null)
            {
                return;
            }

            TryBindRefineryPanelTargets();
            TryBindShipyardPanelTargets();
            _panelBindingsReady = _refineryBindingsReady && _shipyardBindingsReady;
        }

        void TryBindRefineryPanelTargets()
        {
            if (_refineryBindingsReady || refinery.panel == null)
            {
                return;
            }

            _refineryPanelTargets.Clear();
            _refineryOreSlots.Clear();
            CacheRefineryOreSlots();

            for (var i = 0; i < _refineryOreSlots.Count; i++)
            {
                var index = i;
                AddPanelTarget(_refineryPanelTargets, _refineryOreSlots[i].slot,
                    () => _shop.GetOreHoverText(index),
                    () =>
                    {
                        if (_shop.BuyOre(index))
                        {
                            RefreshPanelPresentation(refinery);
                        }
                    });
            }

            AddPanelTarget(_refineryPanelTargets, FindDeepChild(refinery.panel, "删除矿物"),
                () => _shop.GetRemoveOreHoverText(),
                BeginRemoveOreSelection);

            AddPanelTarget(_refineryPanelTargets, FindDeepChild(refinery.panel, "矿物强化"),
                () => _shop.GetUpgradeOreHoverText(-1),
                BeginUpgradeOreSelection);

            AddPanelTarget(_refineryPanelTargets, FindDeepChild(refinery.panel, "刷新商店"),
                () => _shop.GetRefreshRefineryHoverText(),
                () =>
                {
                    if (_shop.RefreshRefinery())
                    {
                        RefreshPanelPresentation(refinery);
                    }
                });

            _refineryBindingsReady = true;
        }

        void TryBindShipyardPanelTargets()
        {
            if (_shipyardBindingsReady || shipyard.panel == null)
            {
                return;
            }

            _shipyardPanelTargets.Clear();
            _shipyardRelicSlots.Clear();
            CacheShipyardRelicSlots();

            for (var i = 0; i < _shipyardRelicSlots.Count; i++)
            {
                var index = i;
                AddPanelTarget(_shipyardPanelTargets, _shipyardRelicSlots[i].transform,
                    () => _shop.GetRelicHoverText(index),
                    () =>
                    {
                        if (_shop.BuyRelic(index))
                        {
                            RefreshPanelPresentation(shipyard);
                        }
                    });
            }

            _shipyardBindingsReady = true;
        }

        void BeginRemoveOreSelection()
        {
            if (oreInventoryPanel == null)
            {
                ShowNotice("未找到矿石库面板。");
                return;
            }

            var deck = RunData.Ensure().Deck;
            if (deck.Count == 0)
            {
                ShowNotice("矿舱已空，无法删除。");
                return;
            }

            oreInventoryPanel.BeginSelection(
                deckIndex =>
                {
                    if (_shop.RemoveOre(deckIndex))
                    {
                        RefreshPanelPresentation(refinery);
                    }
                },
                () => RefreshPanelPresentation(refinery),
                $"选择要删除的矿石（费用 {RunData.Ensure().ShopRemoveCost} 银元）");
        }

        void BeginUpgradeOreSelection()
        {
            if (oreInventoryPanel == null)
            {
                ShowNotice("未找到矿石库面板。");
                return;
            }

            var deck = RunData.Ensure().Deck;
            if (deck.Count == 0)
            {
                ShowNotice("矿舱已空，无法强化。");
                return;
            }

            oreInventoryPanel.BeginSelection(
                deckIndex =>
                {
                    if (_shop.UpgradeOre(deckIndex))
                    {
                        RefreshPanelPresentation(refinery);
                    }
                },
                () => RefreshPanelPresentation(refinery),
                "选择要强化的矿石（+5 强度，费用递增）");
        }

        void CacheRefineryOreSlots()
        {
            var slotsRoot = FindDeepChild(refinery.panel, "商品栏位");
            if (slotsRoot == null)
            {
                return;
            }

            for (var i = 0; i < slotsRoot.childCount; i++)
            {
                var child = slotsRoot.GetChild(i);
                var renderer = child.GetComponent<SpriteRenderer>();
                _refineryOreSlots.Add((child, renderer));
            }
        }

        void CacheShipyardRelicSlots()
        {
            if (shipyard.panel == null)
            {
                return;
            }

            for (var i = 1; i <= 3; i++)
            {
                var slot = FindDeepChild(shipyard.panel, $"遗物{i}");
                if (slot == null)
                {
                    continue;
                }

                _shipyardRelicSlots.Add(slot.GetComponent<SpriteRenderer>());
            }
        }

        static void AddPanelTarget(List<PanelTarget> targets, Transform transform, Func<string> getHoverText, Action onClick)
        {
            if (transform == null || onClick == null)
            {
                return;
            }

            var collider = transform.GetComponent<Collider2D>();
            if (collider == null)
            {
                return;
            }

            targets.Add(new PanelTarget
            {
                Collider = collider,
                GetHoverText = getHoverText,
                OnClick = onClick,
            });
        }

        void HandlePanelHover(List<PanelTarget> targets)
        {
            if (targets.Count == 0 || !TryGetPointerWorldPosition(out var worldPoint))
            {
                ClearPanelHover();
                return;
            }

            PanelTarget hit = null;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Collider != null && target.Collider.enabled && target.Collider.OverlapPoint(worldPoint))
                {
                    hit = target;
                    break;
                }
            }

            if (hit == null)
            {
                if (_hoveredPanelTarget != null)
                {
                    _hoveredPanelTarget = null;
                    ShowDefaultShopDescription();
                }

                return;
            }

            if (_hoveredPanelTarget == hit)
            {
                return;
            }

            _hoveredPanelTarget = hit;
            var text = hit.GetHoverText?.Invoke();
            if (!string.IsNullOrEmpty(text))
            {
                ShowShopDescription(text);
            }
        }

        void ClearPanelHover()
        {
            _hoveredPanelTarget = null;
            _hoveredOreDeckIndex = -1;
        }

        void HandleOreInventorySelectionHover()
        {
            if (oreInventoryPanel == null || !oreInventoryPanel.IsOpen)
            {
                return;
            }

            if (oreInventoryPanel.TryGetHoveredDeckIndex(out var deckIndex))
            {
                if (_hoveredOreDeckIndex == deckIndex)
                {
                    return;
                }

                _hoveredOreDeckIndex = deckIndex;
                var text = _shop?.GetDeckOreHoverText(deckIndex);
                if (!string.IsNullOrEmpty(text))
                {
                    ShowShopDescription(text);
                }

                return;
            }

            if (_hoveredOreDeckIndex < 0)
            {
                return;
            }

            _hoveredOreDeckIndex = -1;
            ShowDefaultShopDescription();
        }

        void HandlePanelClicks(List<PanelTarget> targets, ServicePanel panelState)
        {
            if (!panelState.Open || !FlowInput.PrimaryClickThisFrame() || targets.Count == 0)
            {
                return;
            }

            if (!TryGetPointerWorldPosition(out var worldPoint))
            {
                return;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target.Collider != null && target.Collider.enabled && target.Collider.OverlapPoint(worldPoint))
                {
                    target.OnClick?.Invoke();
                    return;
                }
            }
        }

        void HandleShopInput()
        {
            if (_shop == null)
            {
                return;
            }

            if (refinery.Open)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    if (_shop.RefreshRefinery())
                    {
                        RefreshPanelPresentation(refinery);
                    }
                }
            }

            if (shipyard.Open)
            {
                if (Input.GetKeyDown(KeyCode.S))
                {
                    if (_shop.UpgradeSlot())
                    {
                        RefreshPanelPresentation(shipyard);
                    }

                    return;
                }

                for (var i = 0; i < 2; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        if (_shop.EnchantOre(0, i))
                        {
                            RefreshPanelPresentation(shipyard);
                        }

                        return;
                    }
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    if (_shop.RefreshShipyard())
                    {
                        RefreshPanelPresentation(shipyard);
                    }
                }
            }
        }

        void ShowNotice(string text)
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, text, 0f);
        }

        void HideNotice()
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice != null && notice.IsShowing && notice.ActiveChannel == NoticeChannel.Notice)
            {
                notice.Hide();
            }
        }

        void ShowDefaultShopDescription()
        {
            if (_shop == null)
            {
                return;
            }

            if (refinery.Open)
            {
                ShowShopDescription(_shop.GetRefineryPanelHint());
            }
            else if (shipyard.Open)
            {
                ShowShopDescription(_shop.GetShipyardPanelHint());
            }
        }

        void ShowShopDescription(string text)
        {
            CacheShopUi();
            if (_shopDescriptionObject == null)
            {
                return;
            }

            EnsureShopOverlayActive();
            ApplyOverlayText(_shopDescriptionObject, _shopDescriptionText, _shopDescriptionAnimator, text);
        }

        void HideShopDescription()
        {
            if (_shopDescriptionObject != null)
            {
                _shopDescriptionObject.SetActive(false);
            }
        }

        void ShowShopHud()
        {
            CacheShopUi();
            EnsureShopOverlayActive();
            RefreshGoldDisplay();
            _shopHudVisible = true;
        }

        void HideShopHud()
        {
            HideShopDescription();
            ClearPanelHover();
            _shopHudVisible = false;
            TryReleaseShopOverlay();
        }

        void RefreshGoldDisplay()
        {
            PlayerRunHudController.Instance?.RefreshGold();
        }

        void EnsureShopOverlayActive()
        {
            UiSystem.Instance?.SetOverlayActive(true);
        }

        void TryReleaseShopOverlay()
        {
            if (_shopHudVisible || refinery.Open || shipyard.Open)
            {
                return;
            }

            var ui = UiSystem.Instance;
            if (ui == null)
            {
                return;
            }

            ui.SetOverlayActive(false);
        }

        static void ApplyOverlayText(
            GameObject textObject,
            TMP_Text tmp,
            TextAnimator_TMP animator,
            string text)
        {
            if (textObject == null)
            {
                return;
            }

            textObject.SetActive(true);

            var content = text ?? string.Empty;
            if (animator != null && textObject.activeInHierarchy)
            {
                animator.SetText(content, hideText: false);
                animator.SetVisibilityEntireText(true, canPlayEffects: false);
                return;
            }

            if (tmp != null)
            {
                tmp.text = content;
            }
        }

        static void DisableRaycastOnOverlayText(GameObject textObject)
        {
            if (textObject == null)
            {
                return;
            }

            var graphics = textObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }

        void CacheShopUi()
        {
            if (_shopDescriptionObject != null && _goldCountObject != null)
            {
                return;
            }

            var uiRoot = UiSystem.Instance != null ? UiSystem.Instance.transform : null;
            if (uiRoot == null)
            {
                return;
            }

            if (_shopDescriptionObject == null)
            {
                var desc = FindDeepChild(uiRoot, "精炼厂/船坞通用介绍文字框");
                if (desc != null)
                {
                    _shopDescriptionObject = desc.gameObject;
                    _shopDescriptionText = desc.GetComponent<TMP_Text>();
                    _shopDescriptionAnimator = desc.GetComponent<TextAnimator_TMP>();
                    DisableRaycastOnOverlayText(_shopDescriptionObject);
                }
            }

            if (_goldCountObject == null)
            {
                var gold = FindDeepChild(uiRoot, "金币数量");
                if (gold != null)
                {
                    _goldCountObject = gold.gameObject;
                    _goldCountText = gold.GetComponent<TMP_Text>();
                    _goldCountAnimator = gold.GetComponent<TextAnimator_TMP>();
                    DisableRaycastOnOverlayText(_goldCountObject);
                }
            }

            if (_refineryGoldIcon == null && refinery.panel != null)
            {
                _refineryGoldIcon = FindDeepChild(refinery.panel, "金币图标");
            }

            if (_shipyardGoldIcon == null && shipyard.panel != null)
            {
                _shipyardGoldIcon = FindDeepChild(shipyard.panel, "金币图标");
            }
        }

        bool TryGetPointerWorldPosition(out Vector2 worldPoint)
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                worldPoint = default;
                return false;
            }

            var screenPoint = PointerScreenPosition();
            var depth = Mathf.Abs(worldCamera.transform.position.z);
            var world = worldCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
            worldPoint = world;
            return true;
        }

        static Vector2 PointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Input.mousePosition;
        }

        void ResolveRefs(bool forceRefresh = false)
        {
            if (forceRefresh || pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (forceRefresh || refinery.trigger == null)
            {
                refinery.trigger = FindSelectable("精炼厂");
            }

            if (forceRefresh || shipyard.trigger == null)
            {
                shipyard.trigger = FindSelectable("船坞");
            }

            if (forceRefresh || refinery.panel == null)
            {
                refinery.panel = FindPanel("精炼厂panel");
            }

            if (forceRefresh || shipyard.panel == null)
            {
                shipyard.panel = FindPanel("船坞panel");
            }

            if (forceRefresh || sharedEntryPoint == null)
            {
                sharedEntryPoint = FindTransform("panel 入场点位");
            }

            if (forceRefresh || refinery.entryPoint == null)
            {
                refinery.entryPoint = sharedEntryPoint;
            }

            if (forceRefresh || shipyard.entryPoint == null)
            {
                shipyard.entryPoint = sharedEntryPoint;
            }

            if (forceRefresh || leaveElement == null)
            {
                leaveElement = FindSelectable("离开按钮")
                    ?? FindSelectable("离开")
                    ?? FindSelectable("返回地图");
            }

            if (forceRefresh || oreInventoryPanel == null)
            {
                oreInventoryPanel = FindFirstObjectByType<OreInventoryPanel>(FindObjectsInactive.Include);
            }

            if (forceRefresh)
            {
                _shopDescriptionObject = null;
                _goldCountObject = null;
                _shopDescriptionText = null;
                _goldCountText = null;
                _shopDescriptionAnimator = null;
                _goldCountAnimator = null;
                _refineryGoldIcon = null;
                _shipyardGoldIcon = null;
            }

            CacheShopUi();
        }

        bool IsRefineryTrigger(SelectableSceneElement hovered)
        {
            if (hovered == null)
            {
                return false;
            }

            if (refinery.trigger != null && hovered == refinery.trigger)
            {
                return true;
            }

            return MatchesServiceTrigger(hovered, "精炼厂", "factory");
        }

        bool IsShipyardTrigger(SelectableSceneElement hovered)
        {
            if (hovered == null)
            {
                return false;
            }

            if (shipyard.trigger != null && hovered == shipyard.trigger)
            {
                return true;
            }

            return MatchesServiceTrigger(hovered, "船坞", "dockyard");
        }

        static bool MatchesServiceTrigger(SelectableSceneElement hovered, string objectName, string elementId)
        {
            if (hovered == null)
            {
                return false;
            }

            if (string.Equals(hovered.ElementId, elementId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(hovered.name.Trim(), objectName, StringComparison.Ordinal);
        }

        static SelectableSceneElement FindSelectable(string objectName)
        {
            var t = FindTransform(objectName);
            return t != null ? t.GetComponent<SelectableSceneElement>() : null;
        }

        static Transform FindTransform(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.transform : null;
        }

        static Transform FindPanel(string objectName)
        {
            var uiRoot = UiSystem.Instance != null ? UiSystem.Instance.transform : null;
            if (uiRoot != null)
            {
                var found = FindDeepChild(uiRoot, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return FindTransform(objectName);
        }

        static Transform FindDeepChild(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            var all = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].name == objectName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
