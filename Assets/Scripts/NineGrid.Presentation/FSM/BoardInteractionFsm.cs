using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Adaptors;
using NineGrid.Presentation.Performance;
using NineGrid.Presentation.Registry;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NineGrid.Presentation.FSM
{
    public enum BoardInteractionState
    {
        Idle,
        Hover,
        Watching,
    }

    /// <summary>
    /// Phase 2 预留：道具指向 Assist 子态挂点。
    /// </summary>
    public enum BoardInteractionSubMode
    {
        Normal,
        ItemUseAssist,
    }

    /// <summary>
    /// 场地卡交互 FSM：Hover 本地反馈，Confirm 发 Attack/Pickup/ClickEmpty Command。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardInteractionFsm : MonoBehaviour, IController, IFlowShellManagedInteraction
    {
        [Header("References")]
        [SerializeField] private InputLockGate inputLockGate;
        [SerializeField] private BoardItemInteractionCoordinator coordinator;
        [SerializeField] private TableNineViewRegistry viewRegistry;
        [SerializeField] private CardHoverPerformance hoverPerformance;
        [SerializeField] private ContentInfoPresenter infoPresenter;
        [SerializeField] private PresentationBatchPlayer batchPlayer;
        [SerializeField] private Camera inputCamera;

        [Header("Input")]
        [SerializeField] private bool pointerInputEnabled = true;

        private BoardInteractionState state = BoardInteractionState.Idle;
        private BoardInteractionSubMode subMode = BoardInteractionSubMode.Normal;
        private bool isWatching;
        private bool itemDragActive;
        private Transform hoveredActor;
        private SlotId hoveredSlot;
        private CoreCommandDispatcher commandDispatcher;

        public BoardInteractionState State => state;
        public BoardInteractionSubMode SubMode => subMode;

        public void SetFlowShellInteractionEnabled(bool enabled)
        {
            pointerInputEnabled = enabled;
            if (!enabled)
            {
                ClearHover();
                SetState(BoardInteractionState.Idle);
            }
        }

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            commandDispatcher = new CoreCommandDispatcher(GetArchitecture());
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (inputLockGate != null)
            {
                inputLockGate.OnWatchingChanged += HandleWatchingChanged;
                HandleWatchingChanged(inputLockGate.IsWatching);
            }

            if (coordinator != null)
            {
                coordinator.ItemDragBegan += HandleItemDragBegan;
                coordinator.ItemDragEnded += HandleItemDragEnded;
                itemDragActive = coordinator.IsItemDragActive;
            }

            this.RegisterEvent<Evt_ActionRejected>(HandleActionRejected);
        }

        private void OnDisable()
        {
            if (inputLockGate != null)
            {
                inputLockGate.OnWatchingChanged -= HandleWatchingChanged;
            }

            if (coordinator != null)
            {
                coordinator.ItemDragBegan -= HandleItemDragBegan;
                coordinator.ItemDragEnded -= HandleItemDragEnded;
            }

            this.UnRegisterEvent<Evt_ActionRejected>(HandleActionRejected);
            ClearHover();
            SetState(BoardInteractionState.Idle);
        }

        private void Update()
        {
            if (!pointerInputEnabled || !Application.isPlaying)
            {
                return;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            if (inputCamera == null || viewRegistry == null)
            {
                return;
            }

            if (IsPointerOverUi())
            {
                ClearHover();
                return;
            }

            HandlePointerInput();
        }

        private void HandlePointerInput()
        {
            if (isWatching)
            {
                ClearHover();
                return;
            }

            bool canHover = true;
            bool canClick = !itemDragActive && subMode == BoardInteractionSubMode.Normal;

            if (!TryResolvePointerHit(Input.mousePosition, out SlotId slot, out int cardUid, out Transform actor))
            {
                ClearHover();
                return;
            }

            if (canHover)
            {
                if (actor != hoveredActor || slot != hoveredSlot)
                {
                    ClearHover();
                    EnterHover(slot, actor, cardUid);
                }
            }

            if (Input.GetMouseButtonDown(0) && canClick)
            {
                TrySendBoardCommand(slot);
            }
        }

        private void TrySendBoardCommand(SlotId slot)
        {
            if (!slot.IsBoardSlot)
            {
                return;
            }

            Transform rejectActor = hoveredActor;
            ClearHover();

            if (!BoardInteractionRules.TryResolveBoardCommand(GetArchitecture(), slot, out ICommand<CoreCommandResult> command))
            {
                return;
            }

            CoreCommandDispatchResult result = commandDispatcher.Send(command);
            batchPlayer?.PlayDispatchResult(result);

            if (!result.Accepted && rejectActor != null)
            {
                hoverPerformance?.PlayReject(rejectActor);
            }
        }

        private void EnterHover(SlotId slot, Transform actor, int cardUid)
        {
            hoveredSlot = slot;
            hoveredActor = actor;

            if (actor == null && viewRegistry.TryGetSlotAnchor(slot, out Transform anchor) && anchor != null)
            {
                hoveredActor = anchor;
            }

            if (hoveredActor != null)
            {
                hoverPerformance?.Play(hoveredActor);
            }

            if (cardUid > 0)
            {
                infoPresenter?.ShowForCardUid(GetArchitecture(), cardUid);
            }
            else
            {
                infoPresenter?.Clear();
            }

            SetState(isWatching ? BoardInteractionState.Watching : BoardInteractionState.Hover);
        }

        private void ClearHover()
        {
            if (hoveredActor != null)
            {
                hoverPerformance?.StopAndRestore(hoveredActor);
            }

            infoPresenter?.Clear();

            hoveredActor = null;
            hoveredSlot = SlotId.None;

            if (state == BoardInteractionState.Hover)
            {
                SetState(isWatching ? BoardInteractionState.Watching : BoardInteractionState.Idle);
            }
        }

        private bool TryResolvePointerHit(
            Vector2 screenPosition,
            out SlotId slot,
            out int cardUid,
            out Transform actor)
        {
            slot = SlotId.None;
            cardUid = 0;
            actor = null;

            Vector3 world = ScreenToWorld(screenPosition);
            Collider2D[] hits = Physics2D.OverlapPointAll(world);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            var board = this.GetModel<BoardModel>();
            Transform bestActor = null;
            SlotId bestSlot = SlotId.None;
            int bestCardUid = 0;
            var bestSortingOrder = int.MinValue;

            for (var i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                if (!viewRegistry.TryResolveBoardPointerHit(hit.transform, out SlotId resolvedSlot, out int resolvedUid, out Transform resolvedActor))
                {
                    continue;
                }

                SlotId targetSlot = resolvedSlot;
                if (!targetSlot.IsBoardSlot && resolvedUid > 0)
                {
                    targetSlot = BoardInteractionRules.FindSlotForCardUid(board, resolvedUid);
                }

                if (!targetSlot.IsBoardSlot)
                {
                    continue;
                }

                Transform hoverActor = resolvedActor;
                if (hoverActor == null && viewRegistry.TryGetSlotAnchor(targetSlot, out Transform anchor))
                {
                    hoverActor = anchor;
                }

                int sortingOrder = 0;
                if (hoverActor != null)
                {
                    SpriteRenderer renderer = hoverActor.GetComponent<SpriteRenderer>();
                    sortingOrder = renderer != null ? renderer.sortingOrder : 0;
                }

                if (sortingOrder >= bestSortingOrder)
                {
                    bestSortingOrder = sortingOrder;
                    bestActor = hoverActor;
                    bestSlot = targetSlot;
                    bestCardUid = resolvedUid > 0 ? resolvedUid : board.GetCardUid(targetSlot);
                }
            }

            if (!bestSlot.IsBoardSlot)
            {
                return false;
            }

            slot = bestSlot;
            cardUid = bestCardUid;
            actor = bestActor;
            return true;
        }

        private void HandleActionRejected(Evt_ActionRejected evt)
        {
            if (evt.Command != GameCommandKind.Attack
                && evt.Command != GameCommandKind.PickupItem
                && evt.Command != GameCommandKind.ClickEmpty)
            {
                return;
            }

            Transform actor = hoveredActor;
            if (actor == null && evt.Slot.IsBoardSlot && viewRegistry != null)
            {
                int uid = this.GetModel<BoardModel>().GetCardUid(evt.Slot);
                if (uid > 0 && viewRegistry.TryGetActor(uid, out Transform resolved))
                {
                    actor = resolved;
                }
                else if (viewRegistry.TryGetSlotAnchor(evt.Slot, out Transform anchor))
                {
                    actor = anchor;
                }
            }

            hoverPerformance?.PlayReject(actor);
            ClearHover();
            SetState(isWatching ? BoardInteractionState.Watching : BoardInteractionState.Idle);
        }

        private void HandleWatchingChanged(bool watching)
        {
            isWatching = watching;
            if (watching)
            {
                ClearHover();
                SetState(BoardInteractionState.Watching);
                return;
            }

            if (state == BoardInteractionState.Watching)
            {
                SetState(BoardInteractionState.Idle);
            }
        }

        private void HandleItemDragBegan(int _)
        {
            itemDragActive = true;
        }

        private void HandleItemDragEnded()
        {
            itemDragActive = false;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            float zDepth = Mathf.Abs(inputCamera.transform.position.z);
            Vector3 screen = new(screenPosition.x, screenPosition.y, zDepth);
            Vector3 world = inputCamera.ScreenToWorldPoint(screen);
            world.z = 0f;
            return world;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void SetState(BoardInteractionState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
        }
    }
}
