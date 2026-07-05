using System;
using System.Collections.Generic;
using NineGrid.Data;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 路线场景（RouteScene）：Route* 阶段点击浮标进入事件；Event* 阶段在场景内点选事件并结算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteController : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            RouteTravel,
            EventChoice,
            OrePick,
            Result,
        }

        [Header("事件选择（世界物体）")]
        [Tooltip("留空时按名字「事件1」「事件2」「事件3」查找。")]
        [SerializeField] SelectableSceneElement[] eventElements;
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] OreInventoryPanel oreInventoryPanel;

        [Header("事件描述")]
        [TextArea(2, 4)]
        [SerializeField] string routeTravelNotice = "前方海雾弥漫……点击浮标继续航行。";

        Phase _phase = Phase.Idle;
        bool _routeCommitted;
        bool _eventFlowActive;
        int _hoveredOptionIndex = -1;
        List<EventDef> _eventOptions = new();
        EventDef _pendingOreEvent;

        public static RouteController Instance { get; private set; }

        /// <summary>Event* 节点是否仍在等待玩家完成（供 SceneFlowDirector 可选检查）。</summary>
        public bool IsEventFlowActive => _eventFlowActive;

        public event Action EventSelected;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.StateChanged -= OnFlowStateChanged;
            }
        }

        void Start()
        {
            ResolveRefs();
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.StateChanged += OnFlowStateChanged;
            }

            SyncPhase(GameFlowController.Instance != null
                ? GameFlowController.Instance.CurrentState
                : GameFlowState.Route1);
        }

        void OnFlowStateChanged(GameFlowState previous, GameFlowState next)
        {
            SyncPhase(next);
        }

        void SyncPhase(GameFlowState state)
        {
            HideDescription();

            if (IsEventState(state))
            {
                BeginEventPhase(state);
                return;
            }

            if (IsRouteState(state))
            {
                BeginRoutePhase();
                return;
            }

            _phase = Phase.Idle;
            _eventFlowActive = false;
        }

        void BeginRoutePhase()
        {
            _phase = Phase.RouteTravel;
            _routeCommitted = false;
            _eventFlowActive = false;
            _eventOptions.Clear();
            EnsurePendingEventsForNextNode();
            UpdateMarkerVisibility(-1);
        }

        void BeginEventPhase(GameFlowState state)
        {
            _phase = Phase.EventChoice;
            _eventFlowActive = true;
            _routeCommitted = true;
            _eventOptions = EventController.PrepareOptions(state);
            UpdateMarkerVisibility(_eventOptions.Count);
        }

        void Update()
        {
            if (pointerSelector == null)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.RouteTravel:
                    UpdateRouteTravel();
                    break;
                case Phase.EventChoice:
                    UpdateEventChoice();
                    break;
                case Phase.Result:
                    if (FlowInput.PrimaryClickThisFrame())
                    {
                        CompleteEventFlow();
                    }
                    break;
            }
        }

        void UpdateRouteTravel()
        {
            if (_routeCommitted)
            {
                return;
            }

            var hoveredIndex = GetHoveredOptionIndex();
            UpdateHoverNotice(hoveredIndex, buildRouteNotice: true);

            if (hoveredIndex >= 0 && FlowInput.PrimaryClickThisFrame())
            {
                CommitRouteTravel();
            }
        }

        void UpdateEventChoice()
        {
            var hoveredIndex = GetHoveredOptionIndex();
            UpdateHoverNotice(hoveredIndex, buildRouteNotice: false);

            if (hoveredIndex < 0 || !FlowInput.PrimaryClickThisFrame())
            {
                return;
            }

            if (hoveredIndex >= _eventOptions.Count)
            {
                return;
            }

            SelectEventOption(hoveredIndex);
        }

        void CommitRouteTravel()
        {
            if (_routeCommitted)
            {
                return;
            }

            _routeCommitted = true;
            EnsurePendingEventsForNextNode();
            HideDescription();
            EventSelected?.Invoke();
            GameFlowController.Instance?.Advance();
        }

        void EnsurePendingEventsForNextNode()
        {
            var run = RunData.Ensure();
            if (run.PendingEventNames != null && run.PendingEventNames.Count > 0)
            {
                return;
            }

            var flowState = GameFlowController.Instance?.CurrentState ?? GameFlowState.Route1;
            var eventState = MapRouteToEventState(flowState);
            var poolType = RunData.GetEventPoolType(eventState);
            var preview = WebGameData.GeneratePostBattleEvents(poolType);
            run.PendingEventNames.Clear();
            foreach (var e in preview)
            {
                run.PendingEventNames.Add(e.Name);
            }

            RunData.Save();
        }

        void SelectEventOption(int index)
        {
            if (index < 0 || index >= _eventOptions.Count)
            {
                return;
            }

            var ev = _eventOptions[index];
            HideDescription();

            var run = RunData.Ensure();
            run.PendingEventNames.Remove(ev.Name);
            RunData.Save();

            if (EventController.NeedsOreSelection(ev.Effect))
            {
                BeginOreSelection(ev);
                return;
            }

            var message = EventController.ApplyEffect(ev);
            ShowResult(message);
        }

        void BeginOreSelection(EventDef ev)
        {
            _pendingOreEvent = ev;
            _phase = Phase.OrePick;

            if (oreInventoryPanel == null)
            {
                ShowResult(EventController.ApplyEffect(ev));
                return;
            }

            oreInventoryPanel.BeginSelection(
                OnOrePicked,
                OnOreSelectionCancelled,
                $"{ev.Name}\n{ev.Desc}\n点击矿舱中的一块矿石。");
        }

        void OnOrePicked(int deckIndex)
        {
            var ev = _pendingOreEvent;
            _pendingOreEvent = null;
            var message = ev != null ? EventController.ApplyEffect(ev, deckIndex) : "选择完成。";
            ShowResult(message);
        }

        void OnOreSelectionCancelled()
        {
            _pendingOreEvent = null;
            _phase = Phase.EventChoice;
        }

        void ShowResult(string message)
        {
            _phase = Phase.Result;
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, message, 0f);
        }

        void CompleteEventFlow()
        {
            HideDescription();
            _eventOptions.Clear();
            _eventFlowActive = false;
            _phase = Phase.Idle;

            var run = RunData.Ensure();
            run.PendingEventNames.Clear();
            RunData.Save();

            GameFlowController.Instance?.Advance();
        }

        int GetHoveredOptionIndex()
        {
            if (eventElements == null)
            {
                return -1;
            }

            var current = pointerSelector.Hovered;
            for (var i = 0; i < eventElements.Length; i++)
            {
                if (eventElements[i] != null && current == eventElements[i])
                {
                    return i;
                }
            }

            return -1;
        }

        void UpdateHoverNotice(int hoveredIndex, bool buildRouteNotice)
        {
            if (hoveredIndex == _hoveredOptionIndex)
            {
                return;
            }

            _hoveredOptionIndex = hoveredIndex;
            if (hoveredIndex < 0)
            {
                HideDescription();
                return;
            }

            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            if (notice == null)
            {
                return;
            }

            if (buildRouteNotice)
            {
                notice.Show(NoticeChannel.Notice, BuildRoutePreviewNotice(), 0f);
                return;
            }

            if (hoveredIndex < _eventOptions.Count)
            {
                var ev = _eventOptions[hoveredIndex];
                notice.Show(NoticeChannel.Notice, $"{ev.Name}\n{ev.Desc}", 0f);
            }
        }

        string BuildRoutePreviewNotice()
        {
            var run = RunData.Ensure();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(routeTravelNotice);

            if (run.PendingEventNames != null && run.PendingEventNames.Count > 0)
            {
                sb.AppendLine("可能遭遇：");
                for (var i = 0; i < run.PendingEventNames.Count; i++)
                {
                    sb.AppendLine($"  · {run.PendingEventNames[i]}");
                }
            }
            else
            {
                sb.AppendLine("可能遭遇：");
                for (var i = 0; i < run.PendingEventNames.Count; i++)
                {
                    sb.AppendLine($"  · {run.PendingEventNames[i]}");
                }
            }

            sb.AppendLine("点击浮标确认。");
            return sb.ToString();
        }

        void UpdateMarkerVisibility(int activeOptionCount)
        {
            if (eventElements == null)
            {
                return;
            }

            for (var i = 0; i < eventElements.Length; i++)
            {
                if (eventElements[i] == null)
                {
                    continue;
                }

                var show = activeOptionCount < 0 || i < activeOptionCount;
                eventElements[i].gameObject.SetActive(show);
            }
        }

        void HideDescription()
        {
            _hoveredOptionIndex = -1;
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

            if (oreInventoryPanel == null)
            {
                oreInventoryPanel = FindFirstObjectByType<OreInventoryPanel>(FindObjectsInactive.Include);
            }

            if (eventElements == null || eventElements.Length == 0 || AllNull(eventElements))
            {
                var found = new List<SelectableSceneElement>();
                AddIfFound(found, "事件1");
                AddIfFound(found, "事件2");
                AddIfFound(found, "事件3");
                AddIfFound(found, "事件");
                AddIfFound(found, "事件选择");
                eventElements = found.ToArray();
            }
        }

        /// <summary>供 uGUI Button 或世界点击调用。</summary>
        public void SelectEvent()
        {
            CommitRouteTravel();
        }

        static bool IsRouteState(GameFlowState state)
        {
            return state >= GameFlowState.Route1 && state <= GameFlowState.Route6;
        }

        static bool IsEventState(GameFlowState state)
        {
            return state >= GameFlowState.Event1 && state <= GameFlowState.Event6;
        }

        static GameFlowState MapRouteToEventState(GameFlowState routeState)
        {
            return routeState switch
            {
                GameFlowState.Route1 => GameFlowState.Event1,
                GameFlowState.Route2 => GameFlowState.Event2,
                GameFlowState.Route3 => GameFlowState.Event3,
                GameFlowState.Route4 => GameFlowState.Event4,
                GameFlowState.Route5 => GameFlowState.Event5,
                GameFlowState.Route6 => GameFlowState.Event6,
                _ => GameFlowState.Event1,
            };
        }

        static bool AllNull(SelectableSceneElement[] arr)
        {
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                {
                    return false;
                }
            }

            return true;
        }

        static void AddIfFound(List<SelectableSceneElement> list, string objectName)
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
