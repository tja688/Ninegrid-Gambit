using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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
            ShipSail,
            OrePick,
            Result,
        }

        [Header("事件浮标（事件1/2/3…）")]
        [SerializeField] SelectableSceneElement[] eventElements;
        [SerializeField] SceneElementPointerSelector pointerSelector;
        [SerializeField] OreInventoryPanel oreInventoryPanel;
        [Tooltip("留空时按名字「PASS」查找；事件流程中点击直接跳过进入下一场战斗。")]
        [SerializeField] SelectableSceneElement passElement;

        [Header("船只驶向事件")]
        [Tooltip("留空时按名字「player」查找。")]
        [SerializeField] Transform playerShip;
        [SerializeField] float sailDuration = 0.85f;
        [SerializeField] Ease sailEase = Ease.OutCubic;
        [Tooltip("相对事件浮标的世界坐标偏移（如略停在浮标下方）。")]
        [SerializeField] Vector3 sailTargetOffset = new Vector3(0f, -0.35f, 0f);

        [Header("结算")]
        [SerializeField] float resultAutoContinueSeconds = 1.2f;

        Phase _phase = Phase.Idle;
        bool _eventFlowActive;
        int _hoveredOptionIndex = -1;
        List<EventDef> _eventOptions = new();
        EventDef _pendingOreEvent;
        Coroutine _resultRoutine;
        Coroutine _deferAdvanceRoutine;
        Coroutine _sailRoutine;
        Tween _sailTween;
        Vector3 _playerHomePosition;
        bool _playerHomeCaptured;
        bool _resultFinalized;

        public static RouteController Instance { get; private set; }

        public bool IsEventFlowActive => _eventFlowActive;

        public event Action EventSelected;

        void Awake()
        {
            Instance = this;
            CapturePlayerHome();
        }

        void OnDestroy()
        {
            KillSailTween();
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

            if (GameFlowScenes.IsRouteState(state))
            {
                BeginEventSelectionPhase(MapRouteToEventState(state));
                return;
            }

            if (GameFlowScenes.IsEventState(state))
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
            _resultFinalized = false;
            _phase = Phase.EventChoice;
            _eventFlowActive = true;
            _eventOptions = EventController.PrepareOptions(eventState);
            ResetPlayerToHome();
            UpdateMarkerVisibility(_eventOptions.Count);
        }

        void Update()
        {
            if (pointerSelector == null)
            {
                return;
            }

            if (_eventFlowActive && FlowInput.PrimaryClickThisFrame() && TryHandlePassClick())
            {
                return;
            }

            if (_phase == Phase.Idle)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.EventChoice:
                    UpdateEventChoice();
                    break;
                case Phase.OrePick:
                    UpdateOrePick();
                    break;
                case Phase.Result:
                    if (FlowInput.PrimaryClickThisFrame())
                    {
                        CompleteEventFlow();
                    }
                    break;
            }
        }

        void UpdateOrePick()
        {
            if (oreInventoryPanel == null || !oreInventoryPanel.IsSelectionMode)
            {
                if (_pendingOreEvent != null)
                {
                    OnOreSelectionCancelled();
                }
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
            if (index < 0 || index >= _eventOptions.Count || _phase == Phase.ShipSail)
            {
                return;
            }

            var ev = _eventOptions[index];
            HideDescription();
            _phase = Phase.ShipSail;
            CancelSailRoutine();
            _sailRoutine = StartCoroutine(SailThenResolve(index, ev));
        }

        IEnumerator SailThenResolve(int index, EventDef ev)
        {
            if (playerShip != null && sailDuration > 0f)
            {
                var target = GetEventSailTarget(index);
                var completed = false;
                KillSailTween();
                _sailTween = playerShip
                    .DOMove(target, sailDuration)
                    .SetEase(sailEase)
                    .SetUpdate(true)
                    .OnComplete(() => completed = true)
                    .OnKill(() => completed = true);

                yield return new WaitUntil(() => completed);
                KillSailTween();
                playerShip.position = target;
            }

            _sailRoutine = null;
            ResolveSelectedEvent(ev);
        }

        void ResolveSelectedEvent(EventDef ev)
        {
            if (EventController.NeedsOreSelection(ev.Effect))
            {
                BeginOreSelection(ev);
                return;
            }

            ApplyEventAndShowResult(ev);
        }

        void ApplyEventAndShowResult(EventDef ev, int oreIndex = -1)
        {
            var message = EventController.ApplyEffect(ev, oreIndex);
            ConsumePendingEvent(ev);
            ShowResult(message);
        }

        void ConsumePendingEvent(EventDef ev)
        {
            if (ev == null)
            {
                return;
            }

            var run = RunData.Ensure();
            run.PendingEventNames.Remove(ev.Name);
            RunData.Save();
        }

        Vector3 GetEventSailTarget(int index)
        {
            if (eventElements != null && index >= 0 && index < eventElements.Length && eventElements[index] != null)
            {
                return eventElements[index].transform.position + sailTargetOffset;
            }

            return playerShip != null ? playerShip.position : Vector3.zero;
        }

        void BeginOreSelection(EventDef ev)
        {
            _pendingOreEvent = ev;

            if (oreInventoryPanel == null)
            {
                oreInventoryPanel = FindFirstObjectByType<OreInventoryPanel>(FindObjectsInactive.Include);
            }

            var run = RunData.Ensure();
            if (run.Deck.Count == 0
                || oreInventoryPanel == null
                || !oreInventoryPanel.TryBeginSelection(
                    OnOrePicked,
                    OnOreSelectionCancelled,
                    $"{ev.Name}\n{ev.Desc}\n点击矿舱中的一块矿石。"))
            {
                _pendingOreEvent = null;
                ApplyEventAndShowResult(ev);
                return;
            }

            _phase = Phase.OrePick;
            UpdateMarkerVisibility(0);
            HideDescription();
        }

        void OnOrePicked(int deckIndex)
        {
            var ev = _pendingOreEvent;
            _pendingOreEvent = null;
            UpdateMarkerVisibility(_eventOptions.Count);
            if (ev == null)
            {
                ShowResult("选择完成。");
                return;
            }

            ApplyEventAndShowResult(ev, deckIndex);
        }

        void OnOreSelectionCancelled()
        {
            _pendingOreEvent = null;
            _phase = Phase.EventChoice;
            UpdateMarkerVisibility(_eventOptions.Count);
        }

        void ShowResult(string message)
        {
            _resultFinalized = false;
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
            if (_phase != Phase.Result || _resultFinalized)
            {
                return;
            }

            _resultFinalized = true;
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

            if (!GameFlowScenes.IsRouteState(flow.CurrentState))
            {
                Debug.LogWarning(
                    $"[Route] CompleteEventFlow: 当前 {flow.CurrentState} 不是 Route*，忽略重复结算。");
                return;
            }

            RunData.Save();
            EventSelected?.Invoke();
            // Route* 已完成真实事件；跳过 Event* 跳板，一次进入下一场战斗。
            flow.AdvanceFromRouteAfterEvent();
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
            CancelSailRoutine();
            if (_deferAdvanceRoutine != null)
            {
                StopCoroutine(_deferAdvanceRoutine);
                _deferAdvanceRoutine = null;
            }
        }

        void CancelSailRoutine()
        {
            if (_sailRoutine != null)
            {
                StopCoroutine(_sailRoutine);
                _sailRoutine = null;
            }

            KillSailTween();
        }

        void KillSailTween()
        {
            if (_sailTween != null && _sailTween.IsActive())
            {
                _sailTween.Kill();
            }

            _sailTween = null;
        }

        void CapturePlayerHome()
        {
            if (playerShip == null)
            {
                var go = GameObject.Find("player");
                if (go != null)
                {
                    playerShip = go.transform;
                }
            }

            if (playerShip != null)
            {
                _playerHomePosition = playerShip.position;
                _playerHomeCaptured = true;
            }
        }

        void ResetPlayerToHome()
        {
            if (playerShip == null)
            {
                CapturePlayerHome();
            }

            if (playerShip != null && _playerHomeCaptured)
            {
                KillSailTween();
                playerShip.position = _playerHomePosition;
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

            if (playerShip == null)
            {
                var go = GameObject.Find("player");
                if (go != null)
                {
                    playerShip = go.transform;
                }
            }

            if (!_playerHomeCaptured)
            {
                CapturePlayerHome();
            }

            if (eventElements == null || eventElements.Length == 0 || AllNull(eventElements))
            {
                var found = new List<SelectableSceneElement>();
                AddIfFound(found, "事件1");
                AddIfFound(found, "事件2");
                AddIfFound(found, "事件3");
                eventElements = found.ToArray();
            }

            if (passElement == null)
            {
                passElement = FindPassElement();
            }
        }

        /// <summary>场景 PASS 按钮：跳过当前事件流程，直接进入下一场战斗。</summary>
        public void ForcePassToNextBattle()
        {
            if (!_eventFlowActive)
            {
                return;
            }

            oreInventoryPanel?.AbortSelectionSilently();
            _pendingOreEvent = null;
            CancelDeferredWork();
            HideDescription();

            var run = RunData.Ensure();
            run.PendingEventNames.Clear();
            RunData.Save();

            ResetEventUi();
            _eventFlowActive = false;
            _phase = Phase.Idle;

            var flow = GameFlowController.Instance;
            if (flow == null)
            {
                Debug.LogWarning("[Route] ForcePass: GameFlowController 不存在。");
                return;
            }

            if (!GameFlowScenes.IsRouteState(flow.CurrentState))
            {
                flow.Advance();
                return;
            }

            EventSelected?.Invoke();
            flow.AdvanceFromRouteAfterEvent();
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

        bool TryHandlePassClick()
        {
            var pass = passElement ?? FindPassElement();
            if (pass == null || !pass.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (pointerSelector.Hovered != pass)
            {
                return false;
            }

            ForcePassToNextBattle();
            return true;
        }

        static SelectableSceneElement FindPassElement()
        {
            var go = GameObject.Find("PASS");
            return go != null ? go.GetComponent<SelectableSceneElement>() : null;
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
