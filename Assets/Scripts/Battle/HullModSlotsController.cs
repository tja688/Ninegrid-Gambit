using System.Collections.Generic;
using NineGrid.GameFlow;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.Battle
{
    /// <summary>
    /// 船体强化槽位：挂在「船体强化槽位」父物体上，统一管理其下各槽位的悬停提示。
    /// 运行时为每个槽位补一个 <see cref="SelectableSceneElement"/>（若无），鼠标移上去在通知文字里显示该槽信息。
    /// 真实船体改造数据尚未接入，先用占位文案。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HullModSlotsController : MonoBehaviour
    {
        [Header("引用（留空取自身子物体）")]
        [SerializeField] Transform slotsRoot;
        [SerializeField] SceneElementPointerSelector pointerSelector;

        [Header("提示")]
        [Tooltip("悬停槽位时显示的占位文案（{0} 为槽位序号）。")]
        [SerializeField] string messageFormat = "船体强化槽位 {0}：空置（改造数据待接入）";

        readonly List<SelectableSceneElement> _slots = new List<SelectableSceneElement>(8);
        SelectableSceneElement _current;

        void Awake()
        {
            if (slotsRoot == null)
            {
                slotsRoot = transform;
            }

            EnsureSlots();
            SetVisible(false);
        }

        /// <summary>控制槽位组显隐（非战斗阶段隐藏）。</summary>
        public void SetVisible(bool visible)
        {
            if (slotsRoot == null)
            {
                slotsRoot = transform;
            }

            slotsRoot.gameObject.SetActive(visible);
        }

        void Start()
        {
            if (pointerSelector == null)
            {
                pointerSelector = FindFirstObjectByType<SceneElementPointerSelector>();
            }
        }

        void OnDisable()
        {
            HideIfMine();
        }

        void Update()
        {
            if (pointerSelector == null)
            {
                return;
            }

            // 锻造场景开着时不打扰（船体被液压场景盖住）。
            var forge = HydraulicSceneController.Instance;
            var forgeUp = forge != null && (forge.IsActive || forge.IsBusy);
            var hovered = forgeUp ? null : pointerSelector.Hovered;

            var slotHover = _slots.Contains(hovered) ? hovered : null;
            if (slotHover == _current)
            {
                return;
            }

            _current = slotHover;
            if (_current != null)
            {
                var index = _slots.IndexOf(_current) + 1;
                ShowNotice(string.Format(messageFormat, index));
            }
            else
            {
                HideIfMine();
            }
        }

        void EnsureSlots()
        {
            _slots.Clear();
            if (slotsRoot == null)
            {
                return;
            }

            for (var i = 0; i < slotsRoot.childCount; i++)
            {
                var child = slotsRoot.GetChild(i);
                if (child.GetComponent<SpriteRenderer>() == null)
                {
                    continue;
                }

                var selectable = child.GetComponent<SelectableSceneElement>();
                if (selectable == null)
                {
                    selectable = child.gameObject.AddComponent<SelectableSceneElement>();
                }

                _slots.Add(selectable);
            }
        }

        void ShowNotice(string text)
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, text, 0f);
        }

        void HideIfMine()
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice != null && notice.IsShowing && notice.ActiveChannel == NoticeChannel.Notice)
            {
                notice.Hide();
            }
        }
    }
}
