using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 手牌道具交互 FSM：idle / hover / drag / 回手；经 Gateway 发 <see cref="UseItemCommand"/>。
    /// </summary>
    public sealed class HandInteractionFsm
    {
        public event Action<int> ItemUseDispatched;

        public HandInteractionState State { get; private set; } = HandInteractionState.Idle;
        public bool InputLocked { get; set; }
        public int HoveredItemUid { get; private set; }
        public int DraggedItemUid { get; private set; }

        private HandCardDragPresenter mDragPresenter;
        private HandCardReturnPresenter mReturnPresenter;
        private HandLayoutPresenter mLayoutPresenter;
        private CommandGateway mGateway;
        private IArchitecture mArchitecture;
        private Func<bool> mIsInputAllowed;
        private Func<IReadOnlyList<Transform>> mHandActorsProvider;
        private Func<Vector3, bool> mUseZoneContains;

        private Transform mHoveredActor;
        private Transform mDragActor;
        private HandCardLayoutTarget mDragHomeTarget;
        private bool mDragHomeKnown;

        public void Bind(
            HandCardDragPresenter dragPresenter,
            HandCardReturnPresenter returnPresenter,
            HandLayoutPresenter layoutPresenter,
            CommandGateway gateway,
            IArchitecture architecture,
            Func<bool> isInputAllowed,
            Func<IReadOnlyList<Transform>> handActorsProvider,
            Func<Vector3, bool> useZoneContains)
        {
            mDragPresenter = dragPresenter;
            mReturnPresenter = returnPresenter;
            mLayoutPresenter = layoutPresenter;
            mGateway = gateway;
            mArchitecture = architecture;
            mIsInputAllowed = isInputAllowed;
            mHandActorsProvider = handActorsProvider;
            mUseZoneContains = useZoneContains;
        }

        public void NotifyHover(int itemUid, Transform actor)
        {
            if (!CanAcceptInput() || itemUid <= 0 || actor == null || !IsItemUid(itemUid))
            {
                return;
            }

            if (State == HandInteractionState.Drag)
            {
                return;
            }

            if (State == HandInteractionState.Hover && HoveredItemUid == itemUid)
            {
                return;
            }

            StopHoverVisual(restoreFocused: true);
            HoveredItemUid = itemUid;
            mHoveredActor = actor;
            State = HandInteractionState.Hover;
            mDragPresenter?.PlayFocus(actor, CollectOtherActors(actor));
        }

        public void NotifyHoverExit(int itemUid)
        {
            if (!CanAcceptInput() || State == HandInteractionState.Drag)
            {
                return;
            }

            if (HoveredItemUid != itemUid)
            {
                return;
            }

            StopHoverVisual(restoreFocused: true);
            ResetHoverState();
        }

        public void NotifyPress(int itemUid, Transform actor, Vector3 pointerWorldPosition)
        {
            if (!CanAcceptInput() || itemUid <= 0 || actor == null || !IsItemUid(itemUid))
            {
                return;
            }

            HoveredItemUid = itemUid;
            mHoveredActor = actor;
            DraggedItemUid = itemUid;
            mDragActor = actor;
            State = HandInteractionState.Drag;
            RememberDragHome(actor);
            mDragPresenter?.BeginDrag(actor, pointerWorldPosition);
        }

        public void NotifyDrag(Vector3 pointerWorldPosition)
        {
            if (!CanAcceptInput() || State != HandInteractionState.Drag || mDragActor == null)
            {
                return;
            }

            bool inZone = mUseZoneContains != null && mUseZoneContains(pointerWorldPosition);
            mDragPresenter?.UpdateDrag(mDragActor, pointerWorldPosition, inZone);
        }

        public void NotifyRelease(Vector3 pointerWorldPosition)
        {
            if (!CanAcceptInput() || State != HandInteractionState.Drag || mDragActor == null)
            {
                return;
            }

            bool inZone = mUseZoneContains != null && mUseZoneContains(pointerWorldPosition);
            int itemUid = DraggedItemUid;
            Transform actor = mDragActor;

            mDragPresenter?.EndDrag();

            if (inZone && itemUid > 0 && CanUseItem(itemUid))
            {
                mGateway?.Send(new UseItemCommand(itemUid));
                ItemUseDispatched?.Invoke(itemUid);
            }
            else if (inZone)
            {
                mDragPresenter?.PlayReject(actor);
                PlayReturn(actor);
            }
            else
            {
                PlayReturn(actor);
            }

            ResetDragState();
            ResetHoverState();
            State = HandInteractionState.Idle;
        }

        public void ForceReset()
        {
            if (State == HandInteractionState.Drag && mDragActor != null)
            {
                mDragPresenter?.EndDrag();
                PlayReturn(mDragActor, immediate: true);
            }
            else
            {
                StopHoverVisual(restoreFocused: true);
            }

            mDragPresenter?.ForceReset();
            ResetDragState();
            ResetHoverState();
            State = HandInteractionState.Idle;
        }

        private void RememberDragHome(Transform actor)
        {
            mDragHomeKnown = false;
            if (mLayoutPresenter == null || actor == null)
            {
                return;
            }

            mLayoutPresenter.EnsureBaseline(actor);
            if (mLayoutPresenter.TryGetState(actor, out HandLayoutPresenter.ActorVisualState state))
            {
                mDragHomeTarget = new HandCardLayoutTarget
                {
                    LocalPosition = state.BaselineLocalPosition,
                    SortingOrder = state.BaselineSortingOrder,
                };
                mDragHomeKnown = true;
            }
        }

        private void PlayReturn(Transform actor, bool immediate = false)
        {
            if (actor == null || mReturnPresenter == null)
            {
                return;
            }

            if (!mDragHomeKnown)
            {
                mLayoutPresenter?.RestoreActorVisual(actor, immediate ? 0f : -1f);
                return;
            }

            mReturnPresenter.PlayReturn(
                actor,
                mDragHomeTarget.LocalPosition,
                mDragHomeTarget.SortingOrder,
                null);
        }

        private bool CanUseItem(int itemUid)
        {
            if (mArchitecture == null || itemUid <= 0)
            {
                return false;
            }

            return mArchitecture.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.UseItem)
                && IsItemUid(itemUid);
        }

        private bool IsItemUid(int itemUid)
        {
            if (mArchitecture == null || itemUid <= 0)
            {
                return false;
            }

            CoreViewSnapshot snapshot = CoreViewSnapshotFactory.Capture(mArchitecture);
            if (!snapshot.TryGetCard(itemUid, out CardView card))
            {
                return false;
            }

            return card.Zone == ZoneId.ItemSlots;
        }

        private IReadOnlyList<Transform> CollectOtherActors(Transform focused)
        {
            IReadOnlyList<Transform> actors = mHandActorsProvider?.Invoke();
            if (actors == null || actors.Count == 0)
            {
                return Array.Empty<Transform>();
            }

            var others = new List<Transform>();
            for (var i = 0; i < actors.Count; i++)
            {
                Transform actor = actors[i];
                if (actor != null && actor != focused)
                {
                    others.Add(actor);
                }
            }

            return others;
        }

        private bool CanAcceptInput()
        {
            if (InputLocked)
            {
                return false;
            }

            return mIsInputAllowed == null || mIsInputAllowed();
        }

        private void StopHoverVisual(bool restoreFocused)
        {
            if (mHoveredActor == null)
            {
                return;
            }

            mDragPresenter?.StopFocus(
                mHoveredActor,
                CollectOtherActors(mHoveredActor),
                immediate: true,
                restoreFocused: restoreFocused);
        }

        private void ResetHoverState()
        {
            HoveredItemUid = 0;
            mHoveredActor = null;
            if (State != HandInteractionState.Drag)
            {
                State = HandInteractionState.Idle;
            }
        }

        private void ResetDragState()
        {
            DraggedItemUid = 0;
            mDragActor = null;
            mDragHomeKnown = false;
        }
    }
}
