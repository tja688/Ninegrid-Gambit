using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Adaptors;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// 覆盖层交互 FSM：在 Reward / Room / RoomEvent 屏与道具 OptionOverlay 期间处理悬停与 Confirm 发 Command。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectionOverlayFsm : MonoBehaviour, IController, IFlowShellManagedInteraction
    {
        private static readonly string[] StatBoostOptions = { "Attack", "Armor", "Hp" };

        [Header("References")]
        [SerializeField] private SelectionOverlayController overlayController;
        [SerializeField] private InputLockGate inputLockGate;
        [SerializeField] private PresentationBatchPlayer batchPlayer;
        [SerializeField] private InGameFlowShellFsm flowShellFsm;
        [SerializeField] private Camera inputCamera;

        private CoreCommandDispatcher commandDispatcher;
        private bool pointerInputEnabled;
        private bool isWatching;
        private bool awaitingKernelDismiss;
        private bool itemOverlaySessionActive;
        private Action<string> itemOptionSelectedCallback;
        private Action itemOptionCancelledCallback;

        public bool IsPointerInputEnabled => pointerInputEnabled;

        public IArchitecture GetArchitecture()
        {
            return NineGridArchitecture.Interface;
        }

        private void Awake()
        {
            commandDispatcher = new CoreCommandDispatcher(GetArchitecture());
            if (overlayController == null)
            {
                overlayController = GetComponent<SelectionOverlayController>();
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (overlayController != null)
            {
                overlayController.SelectionCommitted += HandleSelectionCommitted;
            }

            if (inputLockGate != null)
            {
                inputLockGate.OnWatchingChanged += HandleWatchingChanged;
                HandleWatchingChanged(inputLockGate.IsWatching);
            }

            if (flowShellFsm != null)
            {
                flowShellFsm.ScreenChanged += HandleScreenChanged;
            }
        }

        private void OnDisable()
        {
            if (overlayController != null)
            {
                overlayController.SelectionCommitted -= HandleSelectionCommitted;
            }

            if (inputLockGate != null)
            {
                inputLockGate.OnWatchingChanged -= HandleWatchingChanged;
            }

            if (flowShellFsm != null)
            {
                flowShellFsm.ScreenChanged -= HandleScreenChanged;
            }
        }

        public void SetFlowShellInteractionEnabled(bool enabled)
        {
            pointerInputEnabled = enabled;
            if (!enabled)
            {
                overlayController?.ClearSession();
                awaitingKernelDismiss = false;
            }
            else
            {
                TryBootstrapOverlayForScreen(flowShellFsm != null ? flowShellFsm.Screen : FlowShellScreen.Idle);
            }
        }

        public bool TryBeginItemStatBoostChoice(int itemUid, Action<string> onSelected, Action onCancelled)
        {
            if (overlayController == null || overlayController.IsBusy)
            {
                return false;
            }

            itemOptionSelectedCallback = onSelected;
            itemOptionCancelledCallback = onCancelled;
            itemOverlaySessionActive = true;
            overlayController.BeginItemOptionSession(itemUid, StatBoostOptions);
            StartCoroutine(overlayController.PlayEntranceCoroutine());
            return true;
        }

        /// <summary>
        /// 道具 OptionOverlay 入口：stat_boost 等需要选项的道具先走覆盖层，最右侧 Pass 取消使用。
        /// </summary>
        public bool TryUseItemWithOptionOverlay(int itemUid, string defId)
        {
            if (defId != "help.stat_boost_card")
            {
                return false;
            }

            return TryBeginItemStatBoostChoice(
                itemUid,
                option => DispatchKernelCommand(new UseItemCommand(itemUid, null, option)),
                () => { });
        }

        public void NotifyKernelOfferStarted()
        {
            awaitingKernelDismiss = false;
        }

        public void NotifyKernelDismissStarted()
        {
            awaitingKernelDismiss = true;
        }

        public void NotifyKernelDismissFinished()
        {
            awaitingKernelDismiss = false;
        }

        public void TryShowEnterRoomIfNeeded()
        {
            if (flowShellFsm != null)
            {
                TryBootstrapOverlayForScreen(flowShellFsm.Screen);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying || overlayController == null)
            {
                return;
            }

            if (!pointerInputEnabled && !itemOverlaySessionActive)
            {
                return;
            }

            if (isWatching || awaitingKernelDismiss || !overlayController.IsInteractive)
            {
                return;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            Vector3 screen = Input.mousePosition;
            overlayController.UpdatePointerHover(screen);

            if (Input.GetMouseButtonDown(0))
            {
                overlayController.TryCommitPointerClick(screen);
            }
        }

        private void HandleWatchingChanged(bool watching)
        {
            isWatching = watching;
        }

        private void HandleScreenChanged(FlowShellScreen screen)
        {
            if (!pointerInputEnabled)
            {
                return;
            }

            TryBootstrapOverlayForScreen(screen);
        }

        private void TryBootstrapOverlayForScreen(FlowShellScreen screen)
        {
            if (overlayController == null || overlayController.IsBusy || awaitingKernelDismiss)
            {
                return;
            }

            if (screen != FlowShellScreen.RoomEventScreen)
            {
                return;
            }

            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.SelectedRoom.Value == RoomKind.None)
            {
                return;
            }

            if (!this.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.EnterRoom))
            {
                return;
            }

            overlayController.BeginEnterRoomSession(pending.SelectedRoom.Value);
            StartCoroutine(overlayController.PlayEntranceCoroutine());
        }

        private void HandleSelectionCommitted(int index, SelectionOverlayOptionDescriptor descriptor)
        {
            if (overlayController == null || descriptor == null)
            {
                return;
            }

            switch (overlayController.SessionKind)
            {
                case SelectionOverlaySessionKind.RewardChoice:
                    DispatchRewardChoice(index, descriptor);
                    break;
                case SelectionOverlaySessionKind.RoomChoice:
                    DispatchRoomChoice(index, descriptor);
                    break;
                case SelectionOverlaySessionKind.EnterRoom:
                    DispatchEnterRoom(index, descriptor);
                    break;
                case SelectionOverlaySessionKind.ItemOption:
                    DispatchItemOption(index, descriptor);
                    break;
            }
        }

        private void DispatchRewardChoice(int index, SelectionOverlayOptionDescriptor descriptor)
        {
            ICommand<CoreCommandResult> command = descriptor.IsPass
                ? new SkipHelpChoiceCommand()
                : new SelectRewardCommand(index);

            DispatchKernelCommand(command);
        }

        private void DispatchRoomChoice(int index, SelectionOverlayOptionDescriptor descriptor)
        {
            DispatchKernelCommand(new SelectRoomCommand(index));
        }

        private void DispatchEnterRoom(int index, SelectionOverlayOptionDescriptor descriptor)
        {
            DispatchKernelCommand(new EnterRoomCommand());
        }

        private void DispatchItemOption(int index, SelectionOverlayOptionDescriptor descriptor)
        {
            var selected = itemOptionSelectedCallback;
            itemOptionSelectedCallback = null;
            itemOptionCancelledCallback = null;
            itemOverlaySessionActive = false;
            string payload = descriptor.Payload;
            StartCoroutine(PlayItemOptionDismissAndCallback(index, payload, selected));
        }

        private System.Collections.IEnumerator PlayItemOptionDismissAndCallback(
            int selectedIndex,
            string payload,
            Action<string> callback)
        {
            yield return overlayController.PlayDismissCoroutine(selectedIndex);
            callback?.Invoke(payload);
        }

        private void DispatchKernelCommand(ICommand<CoreCommandResult> command)
        {
            awaitingKernelDismiss = true;
            CoreCommandDispatchResult result = commandDispatcher.Send(command);
            batchPlayer?.PlayDispatchResult(result);
            if (!result.Accepted)
            {
                awaitingKernelDismiss = false;
                overlayController?.ClearSession();
            }
        }
    }
}
