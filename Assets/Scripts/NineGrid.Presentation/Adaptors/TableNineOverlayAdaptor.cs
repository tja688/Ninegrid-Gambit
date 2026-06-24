using System;
using System.Collections;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.FSM;
using NineGrid.Presentation.Performance;
using UnityEngine;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 覆盖层适配器：认领 OfferReward / OfferRooms / SelectReward / SkipReward / SelectRoom / ResolveRoom，
    /// 驱动 <see cref="SelectionOverlayController"/> 与选择层原子表演。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableNineOverlayAdaptor : MonoBehaviour
    {
        private static readonly HashSet<PresentationInstructionKind> HandledKinds = new()
        {
            PresentationInstructionKind.OfferReward,
            PresentationInstructionKind.OfferRooms,
            PresentationInstructionKind.SelectReward,
            PresentationInstructionKind.SkipReward,
            PresentationInstructionKind.SelectRoom,
            PresentationInstructionKind.ResolveRoom,
        };

        [Header("Overlay")]
        [SerializeField] private SelectionOverlayController overlayController;
        [SerializeField] private SelectionOverlayFsm overlayFsm;

        public bool CanHandle(PresentationInstruction instruction)
        {
            return instruction != null && HandledKinds.Contains(instruction.Kind);
        }

        public IEnumerator PlayInstruction(
            PresentationInstruction instruction,
            IReadOnlyList<PresentationInstruction> batchInstructions,
            CoreViewSnapshot snapshot)
        {
            if (!CanHandle(instruction) || snapshot == null)
            {
                yield break;
            }

            EnsureReferences();

            switch (instruction.Kind)
            {
                case PresentationInstructionKind.OfferReward:
                    yield return PlayOfferReward(instruction, snapshot);
                    break;
                case PresentationInstructionKind.OfferRooms:
                    yield return PlayOfferRooms(snapshot);
                    break;
                case PresentationInstructionKind.SelectReward:
                    yield return PlaySelectReward(instruction);
                    break;
                case PresentationInstructionKind.SkipReward:
                    yield return PlaySkipReward();
                    break;
                case PresentationInstructionKind.SelectRoom:
                    yield return PlaySelectRoom(instruction);
                    break;
                case PresentationInstructionKind.ResolveRoom:
                    yield return PlayResolveRoom(instruction);
                    break;
            }
        }

        private IEnumerator PlayOfferReward(PresentationInstruction instruction, CoreViewSnapshot snapshot)
        {
            overlayFsm?.NotifyKernelOfferStarted();
            string poolId = instruction?.Event != null
                ? SelectionOverlaySkipRules.ExtractRewardPoolId(instruction.Event.Message)
                : string.Empty;
            overlayController.BeginRewardSession(snapshot, poolId);
            yield return overlayController.PlayEntranceCoroutine();
        }

        private IEnumerator PlayOfferRooms(CoreViewSnapshot snapshot)
        {
            overlayFsm?.NotifyKernelOfferStarted();
            overlayController.BeginRoomSession(snapshot);
            yield return overlayController.PlayEntranceCoroutine();
        }

        private IEnumerator PlaySelectReward(PresentationInstruction instruction)
        {
            overlayFsm?.NotifyKernelDismissStarted();
            int selectedIndex = instruction.Event != null ? instruction.Event.Amount : 0;
            yield return overlayController.PlayDismissCoroutine(selectedIndex);
            overlayFsm?.NotifyKernelDismissFinished();
            overlayFsm?.TryShowEnterRoomIfNeeded();
        }

        private IEnumerator PlaySkipReward()
        {
            overlayFsm?.NotifyKernelDismissStarted();
            yield return overlayController.PlaySkipDismissCoroutine();
            overlayFsm?.NotifyKernelDismissFinished();
            overlayFsm?.TryShowEnterRoomIfNeeded();
        }

        private IEnumerator PlaySelectRoom(PresentationInstruction instruction)
        {
            overlayFsm?.NotifyKernelDismissStarted();
            int selectedIndex = instruction.Event != null ? instruction.Event.Amount : 0;
            yield return overlayController.PlayDismissCoroutine(selectedIndex);
            overlayFsm?.NotifyKernelDismissFinished();
            overlayFsm?.TryShowEnterRoomIfNeeded();
        }

        private IEnumerator PlayResolveRoom(PresentationInstruction instruction)
        {
            overlayFsm?.NotifyKernelDismissStarted();
            if (overlayController.IsBusy)
            {
                yield return overlayController.PlaySkipDismissCoroutine();
            }

            yield return WaitSeconds(0.05f);
        }

        private void EnsureReferences()
        {
            if (overlayController == null)
            {
                overlayController = GetComponent<SelectionOverlayController>();
            }

            if (overlayFsm == null)
            {
                overlayFsm = GetComponent<SelectionOverlayFsm>();
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
