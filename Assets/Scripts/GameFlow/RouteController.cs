using System;
using System.Collections;
using System.Collections.Generic;
using NineGrid.Data;
using NineGrid.Presentation.Visuals;
using NineGrid.UI;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 路线场景（RouteScene）：浮标即事件选项，悬停看详情，点击选择并结算，完成后进入战斗。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouteController : MonoBehaviour
    {
        enum Phase
        {
            Idle,
            EventChoice,
            OrePick,
            Result,
        }

        [Header("事件浮标（事件1/2/3…）")]
        [SerializeField] SelectableSceneElement[] eventElements;
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] OreInventoryPanel oreInventoryPanel;

        [Header("结算")]
        [SerializeField] float resultAutoContinueSeconds = 1.2f;

        Phase _phase = Phase.Idle;
        bool _eventFlowActive;
        int _hoveredOptionIndex = -1;
        List<EventDef> _eventOptions = new();
        EventDef _pendingOreEvent;
        Coroutine _resultRoutine;
        Coroutine _deferAdvanceRoutine;

        public static RouteController Instance { get; private set; }

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
            CancelDeferredWork();
            HideDescription();

            if (IsRouteState(state))
            {
                BeginEventSelectionPhase(MapRouteToEventState(state));
                return;
            }

            if (IsEventState(state))
            {
                var run = RunData.Ensure();
                if (run.SkipNextEventPresentation)
                {
                    run.SkipNextEventPresentation = false;
                    RunData.Save();
                    ResetEventUi();
                    _phase = Phase.Idle;
                    _eventFlowActive = false;
                    _deferAdvanceRoutine = StartCoroutine(DeferAdvanceNextFrame());
                    return;
                }

                BeginEventSelectionPhase(state);
                return;
            }

            ResetEventUi();
            _phase = Phase.Idle;
            _eventFlowActive = false;
        }

        void BeginEventSelectionPhase(GameFlowState eventState)
        {
            _phase = Phase.EventChoice;
            _eventFlowActive = true;
            _eventOptions = EventController.PrepareOptions(eventState);
            UpdateMarkerVisibility(_eventOptions.Count);
        }

        void Update()
        {
            if (pointerSelector == null || _phase == Phase.Idle)
            {
                return;
            }

            switch (_phase)
            {
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

        void UpdateEventChoice()
        {
            var hoveredIndex = GetHoveredOptionIndex();
            UpdateHoverNotice(hoveredIndex);

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
            UpdateMarkerVisibility(0);

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
            UpdateMarkerVisibility(_eventOptions.Count);
            var message = ev != null ? EventController.ApplyEffect(ev, deckIndex) : "选择完成。";
            ShowResult(message);
        }

        void OnOreSelectionCancelled()
        {
            _pendingOreEvent = null;
            _phase = Phase.EventChoice;
            UpdateMarkerVisibility(_eventOptions.Count);
        }

        void ShowResult(string message)
        {
            _phase = Phase.Result;
            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, message + "\n\n点击继续…", 0f);

            CancelResultRoutine();
            if (resultAutoContinueSeconds > 0f)
            {
                _resultRoutine = StartCoroutine(AutoCompleteAfterResult());
            }
        }

        IEnumerator AutoCompleteAfterResult()
        {
            yield return new WaitForSecondsRealtime(resultAutoContinueSeconds);
            if (_phase == Phase.Result)
            {
                CompleteEventFlow();
            }
        }

        void CompleteEventFlow()
        {
            CancelDeferredWork();
            HideDescription();
            ResetEventUi();

            _eventFlowActive = false;
            _phase = Phase.Idle;

            var run = RunData.Ensure();
            run.PendingEventNames.Clear();

            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                Debug.LogWarning("[Route] CompleteEventFlow: GameFlowController 不存在。");
                return;
            }

            var fromRoute = IsRouteState(flow.CurrentState);
            if (fromRoute)
            {
                run.SkipNextEventPresentation = true;
            }

            RunData.Save();
            EventSelected?.Invoke();
            flow.Advance();
        }

        IEnumerator DeferAdvanceNextFrame()
        {
            yield return null;
            _deferAdvanceRoutine = null;
            GameFlowController.Instance?.Advance();
        }

        void CancelDeferredWork()
        {
            CancelResultRoutine();
            if (_deferAdvanceRoutine != null)
            {
                StopCoroutine(_deferAdvanceRoutine);
                _deferAdvanceRoutine = null;
            }
        }

        void CancelResultRoutine()
        {
            if (_resultRoutine != null)
            {
                StopCoroutine(_resultRoutine);
                _resultRoutine = null;
            }
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
                if (eventElements[i] != null && eventElements[i].gameObject.activeSelf && current == eventElements[i])
                {
                    return i;
                }
            }

            return -1;
        }

        void UpdateHoverNotice(int hoveredIndex)
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

            if (hoveredIndex >= _eventOptions.Count)
            {
                return;
            }

            var notice = UiSystem.Instance != null ? UiSystem.Instance.Notice : null;
            notice?.Show(NoticeChannel.Notice, EventController.BuildHoverText(_eventOptions[hoveredIndex]), 0f);
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

                var show = activeOptionCount > 0 && i < activeOptionCount;
                eventElements[i].gameObject.SetActive(show);
            }
        }

        void ResetEventUi()
        {
            _eventOptions.Clear();
            _pendingOreEvent = null;
            _hoveredOptionIndex = -1;
            UpdateMarkerVisibility(0);
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
                eventElements = found.ToArray();
            }
        }

        /// <summary>供 uGUI Button 调用（兼容旧接线）。</summary>
        public void SelectEvent()
        {
            if (_phase != Phase.EventChoice || _eventOptions.Count == 0)
            {
                return;
            }

            SelectEventOption(0);
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
