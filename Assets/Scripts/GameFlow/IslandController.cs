using DG.Tweening;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 岛屿场景（IslandScene，对应 Web 精炼厂节点）：两个可悬停对象「精炼厂」「船坞」。
    /// 点选某个 → 对应面板从入场点位（左侧）缓动弹出到预设位置；点另一个 → 切换面板，旧面板原路收起。
    /// 场景中的「离开」按钮 → 推进主流程回到地图（下一路线）。
    ///
    /// 服务（删除矿石 / 强化 +5 / 刷新等）暂为占位，仅弹提示；叠牌等级提升按要求先不做。
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

        bool _left;

        void Start()
        {
            // 在 Start 解析：此时持久化 UiSystem 已把本场景重复的 UI 根清掉，面板取自持久化 UI。
            ResolveRefs();
            InitPanel(refinery);
            InitPanel(shipyard);
        }

        void Update()
        {
            if (_left || pointerSelector == null)
            {
                return;
            }

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
        public void OpenRefinery() => SwitchTo(refinery, shipyard);

        /// <summary>供 uGUI Button 调用。</summary>
        public void OpenShipyard() => SwitchTo(shipyard, refinery);

        /// <summary>供 uGUI Button 调用：离开岛屿，推进主流程。</summary>
        public void Leave()
        {
            if (_left)
            {
                return;
            }

            _left = true;
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
