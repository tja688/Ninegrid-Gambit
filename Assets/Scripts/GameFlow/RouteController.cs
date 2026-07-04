using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 路线场景（RouteScene）：若干事件选择物体（如「事件1」「事件2」）。
    /// 悬停时用通知文字显示事件描述，点击任一事件即推进主流程
    /// （Web 端选完事件还要选下一场战斗，这里简化为直接进入下一场战斗）。
    /// 真实事件逻辑（奖励/分支）后续再补，目前仅打通流程。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteController : MonoBehaviour
    {
        [Header("事件选择（世界物体）")]
        [Tooltip("留空时按名字「事件1」「事件2」「事件」查找。")]
        [SerializeField] SelectableSceneElement[] eventElements;
        [SerializeField] SceneElementPointerSelector pointerSelector;

        [Header("事件描述")]
        [TextArea(2, 4)]
        [SerializeField] string eventDescription = "前方海雾弥漫，一处可疑的浮标随浪起伏……点击探查。";

        bool _picked;
        bool _hovering;

        void Start()
        {
            ResolveRefs();
        }

        void Update()
        {
            if (_picked || pointerSelector == null)
            {
                return;
            }

            var hovered = IsEventHovered();
            if (hovered != _hovering)
            {
                _hovering = hovered;
                if (hovered)
                {
                    ShowDescription();
                }
                else
                {
                    HideDescription();
                }
            }

            if (hovered && FlowInput.PrimaryClickThisFrame())
            {
                SelectEvent();
            }
        }

        bool IsEventHovered()
        {
            if (eventElements == null)
            {
                return false;
            }

            var current = pointerSelector.Hovered;
            for (int i = 0; i < eventElements.Length; i++)
            {
                if (eventElements[i] != null && current == eventElements[i])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>供 uGUI Button 或世界点击调用：选择事件并推进流程。</summary>
        public void SelectEvent()
        {
            if (_picked)
            {
                return;
            }

            _picked = true;
            HideDescription();
            GameFlowController.Instance?.Advance();
        }

        void ShowDescription()
        {
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, eventDescription, 0f);
        }

        void HideDescription()
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

            if (eventElements == null || eventElements.Length == 0 || AllNull(eventElements))
            {
                var found = new System.Collections.Generic.List<SelectableSceneElement>();
                AddIfFound(found, "事件1");
                AddIfFound(found, "事件2");
                AddIfFound(found, "事件3");
                AddIfFound(found, "事件");
                AddIfFound(found, "事件选择");
                eventElements = found.ToArray();
            }
        }

        static bool AllNull(SelectableSceneElement[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                {
                    return false;
                }
            }

            return true;
        }

        static void AddIfFound(System.Collections.Generic.List<SelectableSceneElement> list, string objectName)
        {
            var e = FindSelectable(objectName);
            if (e != null && !list.Contains(e))
            {
                list.Add(e);
            }
        }

        static SelectableSceneElement FindSelectable(string objectName)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<SelectableSceneElement>() : null;
        }
    }
}
