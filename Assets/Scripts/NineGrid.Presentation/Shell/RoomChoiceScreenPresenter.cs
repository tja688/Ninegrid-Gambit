using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Visuals;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    public enum RoomChoiceSubState
    {
        Hidden = 0,
        Entering,
        Ready,
        Exiting,
        Done,
    }

    /// <summary>
    /// 房间选择屏：子状态机 + RoomChoice 黑盒入离场。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomChoiceScreenPresenter : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject roomChoicePanel;

        [Header("Cards")]
        [SerializeField] private Transform room1Card;
        [SerializeField] private Transform room1Start;
        [SerializeField] private Transform room1Home;
        [SerializeField] private Transform room1End;
        [SerializeField] private Transform room2Card;
        [SerializeField] private Transform room2Start;
        [SerializeField] private Transform room2Home;
        [SerializeField] private Transform room2End;

        [Header("Selection")]
        [SerializeField] private SelectionFsmOwner selectionOwner;

        [Header("Info")]
        [SerializeField] private ContentInfoPresenter roomInfoPresenter;
        [SerializeField] private TableNineTextOverlayGate overlayGate;

        [Header("Harness Rooms")]
        [SerializeField] private RoomKind harnessRoom1 = RoomKind.Battle;
        [SerializeField] private RoomKind harnessRoom2 = RoomKind.Shop;

        private MainFlowFsm flowFsm;
        private SelectionFsm selectionFsm;
        private SelectionPresentation presentation;
        private IArchitecture architecture;
        private RoomChoiceSubState subState = RoomChoiceSubState.Hidden;
        private int pendingSelectedIndex = -1;
        private Vector3 room1HomeWorld;
        private Vector3 room2HomeWorld;

        public RoomChoiceSubState SubState => subState;
        public event Action<int, RoomKind> RoomChosen;

        private void Awake()
        {
            if (room1Card != null)
            {
                room1HomeWorld = room1Home != null ? room1Home.position : room1Card.position;
            }

            if (room2Card != null)
            {
                room2HomeWorld = room2Home != null ? room2Home.position : room2Card.position;
            }
        }

        public void Bind(
            MainFlowFsm fsm,
            SelectionFsm selection,
            SelectionPresentation selectionPresentation,
            IArchitecture arch = null)
        {
            flowFsm = fsm;
            selectionFsm = selection;
            presentation = selectionPresentation ?? selectionOwner?.Presentation;
            architecture = arch;
            selectionOwner?.Bind(selectionFsm);
            WireRoomOptions();
        }

        public void OnScreenEntered()
        {
            if (roomChoicePanel != null)
            {
                roomChoicePanel.SetActive(true);
            }

            subState = RoomChoiceSubState.Entering;
            selectionFsm?.ActivateChannel(SelectionChannel.RoomChoice);
            selectionFsm.InputLocked = true;
            overlayGate?.ShowRoomInfo(false);

            presentation?.PlayRoomEntrance(
                new[]
                {
                    new SelectionPresentation.RoomMove(room1Card, room1Start, room1HomeWorld),
                    new SelectionPresentation.RoomMove(room2Card, room2Start, room2HomeWorld),
                },
                OnEnterComplete);
        }

        public void OnScreenExited()
        {
            subState = RoomChoiceSubState.Hidden;
            selectionFsm?.Deactivate();
            selectionFsm?.ForceReset();
            presentation?.ForceRoomReset();
            overlayGate?.ShowRoomInfo(false);
            roomInfoPresenter?.Clear();

            if (roomChoicePanel != null)
            {
                roomChoicePanel.SetActive(false);
            }

            ResetCardVisibility();
        }

        public void HandleOptionHovered(int index)
        {
            if (subState != RoomChoiceSubState.Ready)
            {
                return;
            }

            RoomKind kind = ResolveRoomKind(index);
            if (architecture != null)
            {
                roomInfoPresenter?.ShowForRoomKind(architecture, kind);
            }

            overlayGate?.ShowRoomInfo(true);
        }

        public void HandleOptionConfirmed(int index)
        {
            if (subState != RoomChoiceSubState.Ready || index < 0)
            {
                return;
            }

            subState = RoomChoiceSubState.Exiting;
            selectionFsm.InputLocked = true;
            pendingSelectedIndex = index;

            Transform selected = index == 0 ? room1Card : room2Card;
            Transform selectedEnd = index == 0 ? room1End : room2End;
            Transform unselected = index == 0 ? room2Card : room1Card;
            presentation?.PlayRoomExit(selected, selectedEnd, unselected, OnExitComplete);
        }

        private void OnEnterComplete()
        {
            subState = RoomChoiceSubState.Ready;
            if (selectionFsm != null)
            {
                selectionFsm.InputLocked = false;
            }

            overlayGate?.ShowRoomInfo(false);
            WireRoomOptions();
        }

        private void OnExitComplete()
        {
            subState = RoomChoiceSubState.Done;
            overlayGate?.ShowRoomInfo(false);
            roomInfoPresenter?.Clear();

            int index = pendingSelectedIndex;
            RoomKind kind = ResolveRoomKind(index);
            RoomChosen?.Invoke(index, kind);
            flowFsm?.RequestTransition(MainFlowTransition.RoomSelected);
        }

        private void WireRoomOptions()
        {
            if (presentation == null)
            {
                return;
            }

            var actors = new List<SelectionPresentation.RoomOption>(2);
            if (room1Card != null)
            {
                actors.Add(new SelectionPresentation.RoomOption(
                    room1Card,
                    room1Card.localScale,
                    20));
            }

            if (room2Card != null)
            {
                actors.Add(new SelectionPresentation.RoomOption(
                    room2Card,
                    room2Card.localScale,
                    21));
            }

            presentation.SetRoomOptions(actors);
        }

        private RoomKind ResolveRoomKind(int index)
        {
            return index == 0 ? harnessRoom1 : harnessRoom2;
        }

        private void ResetCardVisibility()
        {
            if (room1Card != null)
            {
                room1Card.gameObject.SetActive(true);
            }

            if (room2Card != null)
            {
                room2Card.gameObject.SetActive(true);
            }
        }
    }
}
