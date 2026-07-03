using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Orchestration;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 棋盘交互 FSM：idle / hover / drag / 目标点选；经 <see cref="CommandGateway"/> 发 Attack/Pickup/ClickEmpty。
    /// </summary>
    public sealed class BoardInteractionFsm
    {
        public event Action<SlotId> CommandDispatched;
        public event Action<int, IReadOnlyList<int>> ItemTargetsConfirmed;
        public event Action ItemTargetCancelled;

        public BoardInteractionState State { get; private set; } = BoardInteractionState.Idle;
        public bool InputLocked { get; set; }
        public int HoveredCardUid { get; private set; }
        public SlotId HoveredSlot { get; private set; } = SlotId.None;
        public int PendingItemUid { get; private set; }
        public IReadOnlyList<int> SelectedTargetUids => mItemTargetSelected;

        private BoardCardHoverPresenter mHoverPresenter;
        private CommandGateway mGateway;
        private IArchitecture mArchitecture;
        private TableNineViewRegistry mViewRegistry;
        private Func<bool> mIsInputAllowed;
        private Func<bool> mIsItemTargetInputAllowed;
        private ItemUseRequirement mItemTargetRequirement;

        private Transform mHoveredActor;
        private Transform mDragActor;
        private SlotId mDragSlot = SlotId.None;
        private int mDragCardUid;
        private readonly List<int> mItemTargetSelected = new();
        private readonly List<Transform> mItemTargetActors = new();

        public void Bind(
            BoardCardHoverPresenter hoverPresenter,
            CommandGateway gateway,
            IArchitecture architecture,
            TableNineViewRegistry viewRegistry,
            Func<bool> isInputAllowed,
            Func<bool> isItemTargetInputAllowed = null)
        {
            mHoverPresenter = hoverPresenter;
            mGateway = gateway;
            mArchitecture = architecture;
            mViewRegistry = viewRegistry;
            mIsInputAllowed = isInputAllowed;
            mIsItemTargetInputAllowed = isItemTargetInputAllowed;
        }

        public void BeginItemTargetSelection(int itemUid, ItemUseRequirement requirement)
        {
            ForceReset();
            PendingItemUid = itemUid;
            mItemTargetRequirement = requirement;
            mItemTargetSelected.Clear();
            mItemTargetActors.Clear();
            State = BoardInteractionState.ItemTargetSelect;
        }

        public void CancelItemTargetSelection()
        {
            if (State != BoardInteractionState.ItemTargetSelect)
            {
                return;
            }

            ClearItemTargetVisuals();
            PendingItemUid = 0;
            mItemTargetRequirement = null;
            mItemTargetSelected.Clear();
            mItemTargetActors.Clear();
            State = BoardInteractionState.Idle;
            ItemTargetCancelled?.Invoke();
        }

        public void NotifyItemTargetHover(int cardUid, Transform actor)
        {
            if (!CanAcceptItemTargetInput() || cardUid <= 0 || actor == null)
            {
                return;
            }

            if (!ItemUseTargetValidator.IsValidBoardTarget(mArchitecture, cardUid, mItemTargetRequirement))
            {
                return;
            }

            if (State == BoardInteractionState.ItemTargetSelect
                && HoveredCardUid == cardUid
                && mHoveredActor == actor)
            {
                return;
            }

            ClearHoverVisual();
            HoveredCardUid = cardUid;
            mHoveredActor = actor;
            mHoverPresenter?.Play(actor);
        }

        public void NotifyItemTargetHoverExit(int cardUid)
        {
            if (!CanAcceptItemTargetInput() || HoveredCardUid != cardUid)
            {
                return;
            }

            ClearHoverVisual();
            HoveredCardUid = 0;
            mHoveredActor = null;
        }

        public void NotifyItemTargetPress(int cardUid, Transform actor)
        {
            if (!CanAcceptItemTargetInput() || cardUid <= 0 || actor == null)
            {
                return;
            }

            if (State != BoardInteractionState.ItemTargetSelect || mItemTargetRequirement == null)
            {
                return;
            }

            if (!ItemUseTargetValidator.IsValidBoardTarget(mArchitecture, cardUid, mItemTargetRequirement))
            {
                PlayReject(actor);
                return;
            }

            if (mItemTargetSelected.Contains(cardUid))
            {
                return;
            }

            mItemTargetSelected.Add(cardUid);
            mItemTargetActors.Add(actor);
            mHoverPresenter?.Play(actor);

            if (mItemTargetSelected.Count < mItemTargetRequirement.TargetCount)
            {
                return;
            }

            int itemUid = PendingItemUid;
            var selected = new List<int>(mItemTargetSelected);
            ClearItemTargetVisuals();
            PendingItemUid = 0;
            mItemTargetRequirement = null;
            mItemTargetSelected.Clear();
            mItemTargetActors.Clear();
            State = BoardInteractionState.Idle;
            ItemTargetsConfirmed?.Invoke(itemUid, selected);
        }

        public void NotifyCardHover(int cardUid, Transform actor)
        {
            if (!CanAcceptInput() || cardUid <= 0 || actor == null)
            {
                return;
            }

            if (State == BoardInteractionState.Drag)
            {
                return;
            }

            if (!TryResolveBoardSlotForCard(cardUid, out SlotId slot, out _))
            {
                return;
            }

            if (State == BoardInteractionState.Hover
                && HoveredCardUid == cardUid
                && HoveredSlot == slot)
            {
                return;
            }

            ClearHoverVisual();
            HoveredCardUid = cardUid;
            HoveredSlot = slot;
            mHoveredActor = actor;
            State = BoardInteractionState.Hover;
            mHoverPresenter?.Play(actor);
        }

        public void NotifyCardHoverExit(int cardUid)
        {
            if (!CanAcceptInput() || State == BoardInteractionState.Drag)
            {
                return;
            }

            if (HoveredCardUid != cardUid)
            {
                return;
            }

            ClearHoverVisual();
            ResetHoverState();
        }

        public void NotifySlotHover(SlotId slot, Transform anchor)
        {
            if (!CanAcceptInput() || slot.IsNone || anchor == null)
            {
                return;
            }

            if (State == BoardInteractionState.Drag)
            {
                return;
            }

            if (!IsInteractableEmptySlot(slot))
            {
                return;
            }

            if (State == BoardInteractionState.Hover && HoveredSlot == slot && HoveredCardUid == 0)
            {
                return;
            }

            ClearHoverVisual();
            HoveredCardUid = 0;
            HoveredSlot = slot;
            mHoveredActor = anchor;
            State = BoardInteractionState.Hover;
        }

        public void NotifySlotHoverExit(SlotId slot)
        {
            if (!CanAcceptInput() || State == BoardInteractionState.Drag)
            {
                return;
            }

            if (HoveredSlot != slot || HoveredCardUid != 0)
            {
                return;
            }

            ClearHoverVisual();
            ResetHoverState();
        }

        public void NotifyPress(int cardUid, Transform actor)
        {
            if (!CanAcceptInput() || actor == null)
            {
                return;
            }

            if (cardUid > 0)
            {
                if (!TryResolveBoardSlotForCard(cardUid, out SlotId slot, out _))
                {
                    return;
                }

                BeginDrag(cardUid, slot, actor);
                return;
            }

            if (!HoveredSlot.IsNone && HoveredCardUid == 0 && mHoveredActor != null)
            {
                BeginDrag(0, HoveredSlot, mHoveredActor);
            }
        }

        public void NotifySlotPress(SlotId slot, Transform anchor)
        {
            if (!CanAcceptInput() || slot.IsNone || anchor == null)
            {
                return;
            }

            if (!IsInteractableEmptySlot(slot))
            {
                return;
            }

            BeginDrag(0, slot, anchor);
        }

        public void NotifyDrag(Vector3 pointerWorldPosition)
        {
            if (!CanAcceptInput() || State != BoardInteractionState.Drag)
            {
                return;
            }

            // 棋盘拖拽当前仅作点选确认前的手势占位；升锁时由协调器 ForceReset。
        }

        public void NotifyRelease()
        {
            if (!CanAcceptInput() || State != BoardInteractionState.Drag)
            {
                return;
            }

            TryConfirmDragTarget();
            ForceReset();
        }

        public void ForceReset()
        {
            if (State == BoardInteractionState.ItemTargetSelect)
            {
                CancelItemTargetSelection();
                return;
            }

            ClearHoverVisualImmediate();
            ResetHoverState();
            mDragActor = null;
            mDragSlot = SlotId.None;
            mDragCardUid = 0;
            State = BoardInteractionState.Idle;
        }

        private void BeginDrag(int cardUid, SlotId slot, Transform actor)
        {
            mDragCardUid = cardUid;
            mDragSlot = slot;
            mDragActor = actor;
            HoveredCardUid = cardUid;
            HoveredSlot = slot;
            mHoveredActor = actor;
            State = BoardInteractionState.Drag;
            mHoverPresenter?.Play(actor);
        }

        private void TryConfirmDragTarget()
        {
            if (mDragSlot.IsNone || mGateway == null || mArchitecture == null)
            {
                PlayReject(mDragActor);
                return;
            }

            var phase = mArchitecture.GetSystem<IPhaseSystem>();
            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(mArchitecture);
            BoardSlotView slotView = snapshot.GetSlot(mDragSlot);
            if (slotView == null)
            {
                PlayReject(mDragActor);
                return;
            }

            if (slotView.CardUid > 0)
            {
                if (!IsPlayerReachableSlot(snapshot, mDragSlot))
                {
                    PlayReject(mDragActor);
                    return;
                }

                if (slotView.Kind == CardKind.Monster)
                {
                    if (phase.CanExecute(GameCommandKind.Attack))
                    {
                        Dispatch(new AttackCommand(mDragSlot), mDragSlot);
                        return;
                    }
                }
                else if (phase.CanExecute(GameCommandKind.PickupItem))
                {
                    Dispatch(new PickupItemCommand(mDragSlot), mDragSlot);
                    return;
                }
            }
            else if (phase.CanExecute(GameCommandKind.ClickEmpty))
            {
                Dispatch(new ClickEmptyCommand(mDragSlot), mDragSlot);
                return;
            }

            PlayReject(mDragActor);
        }

        private void Dispatch(ICommand<CoreCommandResult> command, SlotId slot)
        {
            ClearHoverVisualImmediate();
            mGateway.Send(command);
            CommandDispatched?.Invoke(slot);
        }

        private bool TryResolveBoardSlotForCard(int cardUid, out SlotId slot, out BoardSlotView slotView)
        {
            slot = SlotId.None;
            slotView = null;
            if (mArchitecture == null || cardUid <= 0)
            {
                return false;
            }

            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(mArchitecture);
            return TryFindBoardSlot(snapshot, cardUid, out slot, out slotView);
        }

        private bool IsPlayerReachableSlot(CoreViewSnapshot snapshot, SlotId slot)
        {
            if (snapshot == null || slot.IsNone || mArchitecture == null)
            {
                return false;
            }

            IBoardSystem boardSystem = mArchitecture.GetSystem<IBoardSystem>();
            return boardSystem.AreAdjacent(snapshot.AvatarSlot, slot);
        }

        private bool IsInteractableEmptySlot(SlotId slot)
        {
            if (mArchitecture == null || slot.IsNone)
            {
                return false;
            }

            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(mArchitecture);
            BoardSlotView slotView = snapshot.GetSlot(slot);
            if (slotView == null || slotView.CardUid > 0 || slot == snapshot.AvatarSlot)
            {
                return false;
            }

            return IsPlayerReachableSlot(snapshot, slot);
        }

        private static bool TryFindBoardSlot(
            CoreViewSnapshot snapshot,
            int cardUid,
            out SlotId slot,
            out BoardSlotView slotView)
        {
            slot = SlotId.None;
            slotView = null;
            if (snapshot?.BoardSlots == null)
            {
                return false;
            }

            for (var i = 0; i < snapshot.BoardSlots.Count; i++)
            {
                BoardSlotView candidate = snapshot.BoardSlots[i];
                if (candidate.CardUid == cardUid)
                {
                    slot = candidate.Slot;
                    slotView = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool CanAcceptInput()
        {
            if (InputLocked)
            {
                return false;
            }

            return mIsInputAllowed == null || mIsInputAllowed();
        }

        private bool CanAcceptItemTargetInput()
        {
            if (InputLocked || State != BoardInteractionState.ItemTargetSelect)
            {
                return false;
            }

            return mIsItemTargetInputAllowed == null || mIsItemTargetInputAllowed();
        }

        private void ClearItemTargetVisuals()
        {
            ClearHoverVisualImmediate();
        }

        private void ClearHoverVisual()
        {
            if (mHoveredActor != null)
            {
                mHoverPresenter?.StopAndRestore(mHoveredActor);
            }
        }

        private void ClearHoverVisualImmediate()
        {
            mHoverPresenter?.ForceReset();
        }

        private void ResetHoverState()
        {
            HoveredCardUid = 0;
            HoveredSlot = SlotId.None;
            mHoveredActor = null;
            if (State != BoardInteractionState.Drag)
            {
                State = BoardInteractionState.Idle;
            }
        }

        private void PlayReject(Transform actor)
        {
            if (actor == null)
            {
                return;
            }

            mHoverPresenter?.PlayReject(actor);
        }
    }
}
