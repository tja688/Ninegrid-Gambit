using System;
using DG.Tweening;
using NineGrid.Data;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;
using UnityEngine.UI;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 岛屿场景（IslandScene）：精炼厂 / 船坞面板。
    /// 精炼厂：买矿石/遗物、删矿、强化矿、刷新。
    /// 船坞：铸造台强化、附魔、刷新。
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

        [Header("服务面板（精炼厂 / 船坞）")]
        [SerializeField] ServicePanel refinery = new ServicePanel { hoverNotice = "精炼厂：买矿石、精炼服务。" };
        [SerializeField] ServicePanel shipyard = new ServicePanel { hoverNotice = "船坞：船体改造与修整。" };

        [Header("离开")]
        [Tooltip("留空时按名字「离开」/「返回地图」查找。")]
        [SerializeField] SelectableSceneElement leaveElement;

        [Header("引用 / 参数")]
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [Tooltip("面板入场点位（收起处，通常屏幕左外），两个面板共用；留空时按名字「panel 入场点位」查找。")]
        [SerializeField] Transform sharedEntryPoint;
        [SerializeField] float slideDuration = 0.4f;
        [SerializeField] Ease slideInEase = Ease.OutCubic;
        [SerializeField] Ease slideOutEase = Ease.InCubic;
        [Tooltip("入场点位未指定时，展开位置向左偏移量（世界单位）。")]
        [SerializeField] float offscreenLeftOffset = 12f;

        // 商店系统
        ShopController _shop;
        Text _refineryText;
        Text _shipyardText;
        bool _shopInitialized;

        bool _left;

        public event Action RefineryOpened;
        public event Action ShipyardOpened;
        public event Action PanelSwitched;
        public event Action Left;

        void Start()
        {
            // 在 Start 解析：此时持久化 UiSystem 已把本场景重复的 UI 根清掉，面板取自持久化 UI。
            ResolveRefs();
            InitPanel(refinery);
            InitPanel(shipyard);
            InitShop();
        }

        void InitShop()
        {
            if (_shopInitialized) return;
            _shopInitialized = true;

            var run = RunData.Ensure();
            var oreCatalog = LoadOreCatalog();
            var hullModCatalog = LoadHullModCatalog();
            _shop = new ShopController(run, oreCatalog, hullModCatalog);

            // 为面板创建文本组件
            _refineryText = EnsureTextComponent(refinery.panel, "ShopText");
            _shipyardText = EnsureTextComponent(shipyard.panel, "ShopText");

            // 判断当前岛屿类型并生成库存
            var flowState = GameFlowController.Instance?.CurrentState ?? GameFlowState.Island1;
            var isRefinery = RunData.IsRefinery(flowState);
            if (isRefinery)
            {
                if (run.ShopOres.Count == 0)
                    _shop.GenerateRefineryStock();
            }
            else
            {
                if (run.EnchantOptions.Count == 0)
                    _shop.GenerateShipyardStock();
            }
        }

        OreCatalog _cachedOreCatalog;
        OreCatalog LoadOreCatalog()
        {
            if (_cachedOreCatalog != null) return _cachedOreCatalog;
#if UNITY_EDITOR
            _cachedOreCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<OreCatalog>(
                "Assets/ScriptableObjects/Data/OreCatalog.asset");
#endif
            return _cachedOreCatalog;
        }

        HullModCatalog _cachedHullModCatalog;
        HullModCatalog LoadHullModCatalog()
        {
            if (_cachedHullModCatalog != null) return _cachedHullModCatalog;
#if UNITY_EDITOR
            _cachedHullModCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<HullModCatalog>(
                "Assets/ScriptableObjects/Data/HullModCatalog.asset");
#endif
            return _cachedHullModCatalog;
        }

        Text EnsureTextComponent(Transform panel, string childName)
        {
            if (panel == null) return null;
            // 查找现有
            var existing = panel.Find(childName);
            if (existing != null) return existing.GetComponent<Text>();
            // 创建新文本
            var go = new GameObject(childName);
            go.transform.SetParent(panel, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = new Color(0.95f, 0.9f, 0.8f);
            text.supportRichText = true;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-10, -10);
            return text;
        }

        void Update()
        {
            if (_left || pointerSelector == null)
            {
                return;
            }

            // 商店键盘输入（面板打开时）
            HandleShopInput();

            var clicked = FlowInput.PrimaryClickThisFrame();
            var hovered = pointerSelector.Hovered;

            HandleHover(refinery, hovered);
            HandleHover(shipyard, hovered);

            if (!clicked)
            {
                return;
            }

            if (hovered != null && hovered == refinery.trigger)
            {
                OpenRefinery();
            }
            else if (hovered != null && hovered == shipyard.trigger)
            {
                OpenShipyard();
            }
            else if (leaveElement != null && hovered == leaveElement)
            {
                Leave();
            }
        }

        /// <summary>供 uGUI Button 调用。</summary>
        public void OpenRefinery()
        {
            RefineryOpened?.Invoke();
            PanelSwitched?.Invoke();
            SwitchTo(refinery, shipyard);
        }

        /// <summary>供 uGUI Button 调用。</summary>
        public void OpenShipyard()
        {
            ShipyardOpened?.Invoke();
            PanelSwitched?.Invoke();
            SwitchTo(shipyard, refinery);
        }

        /// <summary>供 uGUI Button 调用：离开岛屿，推进主流程。</summary>
        public void Leave()
        {
            if (_left)
            {
                return;
            }

            _left = true;
            Left?.Invoke();
            HideNotice();

            // 面板挂在持久化 UI 上，离开前收起并隐藏，避免带到后续场景。
            HidePanelImmediate(refinery);
            HidePanelImmediate(shipyard);

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
            var isHover = sp.trigger != null && hovered == sp.trigger;
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

        void InitPanel(ServicePanel sp)
        {
            if (sp?.panel == null)
            {
                return;
            }

            // 面板默认关闭（inactive），其当前 position 即预制体作者摆好的「展开位置」。
            sp.ShownPos = sp.shownPoint != null ? sp.shownPoint.position : sp.panel.position;
            sp.EntryPos = sp.entryPoint != null
                ? sp.entryPoint.position
                : sp.ShownPos + Vector3.left * offscreenLeftOffset;

            sp.panel.position = sp.EntryPos;
            sp.panel.gameObject.SetActive(false);
            sp.Open = false;
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
                // 更新面板文本
                UpdatePanelText(sp);
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

        void UpdatePanelText(ServicePanel sp)
        {
            if (_shop == null) return;
            var text = sp == refinery ? _refineryText : _shipyardText;
            if (text == null) return;

            var flowState = GameFlowController.Instance?.CurrentState ?? GameFlowState.Island1;
            var isRefinery = RunData.IsRefinery(flowState);

            if (sp == refinery && isRefinery)
            {
                text.text = _shop.GetRefineryText();
            }
            else if (sp == shipyard && !isRefinery)
            {
                text.text = _shop.GetShipyardText();
            }
            else if (sp == refinery && !isRefinery)
            {
                text.text = "本岛屿为船坞，请前往船坞面板。";
            }
            else if (sp == shipyard && isRefinery)
            {
                text.text = "本岛屿为精炼厂，请前往精炼厂面板。";
            }
        }

        void HandleShopInput()
        {
            if (_shop == null) return;

            var flowState = GameFlowController.Instance?.CurrentState ?? GameFlowState.Island1;
            var isRefinery = RunData.IsRefinery(flowState);

            // 精炼厂键盘
            if (isRefinery && refinery.Open)
            {
                // 1-5: 买矿石
                for (var i = 0; i < 5; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        if (_shop.BuyOre(i)) UpdatePanelText(refinery);
                        return;
                    }
                }
                // 6-8: 买遗物
                for (var i = 0; i < 3; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha6 + i) || Input.GetKeyDown(KeyCode.Keypad6 + i))
                    {
                        if (_shop.BuyRelic(i)) UpdatePanelText(refinery);
                        return;
                    }
                }
                if (Input.GetKeyDown(KeyCode.R))
                {
                    if (_shop.RefreshRefinery()) UpdatePanelText(refinery);
                }
            }

            // 船坞键盘
            if (!isRefinery && shipyard.Open)
            {
                // S: 铸造台强化
                if (Input.GetKeyDown(KeyCode.S))
                {
                    if (_shop.UpgradeSlot()) UpdatePanelText(shipyard);
                    return;
                }
                // 1-2: 附魔
                for (var i = 0; i < 2; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    {
                        // 简化：附魔第一块矿
                        if (_shop.EnchantOre(0, i)) UpdatePanelText(shipyard);
                        return;
                    }
                }
                if (Input.GetKeyDown(KeyCode.R))
                {
                    if (_shop.RefreshShipyard()) UpdatePanelText(shipyard);
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

        void ResolveRefs()
        {
            if (pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }

            if (refinery.trigger == null)
            {
                refinery.trigger = FindSelectable("精炼厂");
            }

            if (shipyard.trigger == null)
            {
                shipyard.trigger = FindSelectable("船坞");
            }

            // 面板位于（持久化的）UI 预制体内、且默认关闭（inactive），需连非激活对象一起深搜。
            if (refinery.panel == null)
            {
                refinery.panel = FindPanel("精炼厂panel");
            }

            if (shipyard.panel == null)
            {
                shipyard.panel = FindPanel("船坞panel");
            }

            if (sharedEntryPoint == null)
            {
                sharedEntryPoint = FindTransform("panel 入场点位");
            }

            if (refinery.entryPoint == null)
            {
                refinery.entryPoint = sharedEntryPoint;
            }

            if (shipyard.entryPoint == null)
            {
                shipyard.entryPoint = sharedEntryPoint;
            }

            if (leaveElement == null)
            {
                leaveElement = FindSelectable("离开按钮")
                    ?? FindSelectable("离开")
                    ?? FindSelectable("返回地图");
            }
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

        /// <summary>在持久化 UI 根下深搜（含未激活）指定名字的面板；找不到再退回全场景查找。</summary>
        static Transform FindPanel(string objectName)
        {
            var uiRoot = UiSystem.Instance != null ? UiSystem.Instance.transform : null;
            if (uiRoot != null)
            {
                var found = FindInChildrenIncludingInactive(uiRoot, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return FindTransform(objectName);
        }

        static Transform FindInChildrenIncludingInactive(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            var all = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
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
