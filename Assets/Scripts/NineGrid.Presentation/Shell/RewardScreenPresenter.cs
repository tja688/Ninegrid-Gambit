using System.Collections.Generic;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    public enum RewardScreenSubState
    {
        Hidden = 0,
        Entering,
        Ready,
        Confirming,
        Done,
    }

    /// <summary>
    /// 通关奖励屏：帮助卡三选一 + PASS 跳过，选项卡来自 StandUISelection 预制体。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardScreenPresenter : MonoBehaviour
    {
        private static readonly string[] HarnessRewardLabels = { "帮助卡 A", "帮助卡 B", "帮助卡 C" };
        private const string PassLabel = "PASS";
        private const int RewardOptionCount = 3;

        [Header("Panel")]
        [SerializeField] private GameObject rewardPanel;
        [SerializeField] private Transform optionsRoot;

        [Header("Cards")]
        [SerializeField] private GameObject standUiSelectionPrefab;
        [SerializeField] private SelectionFanLayout fanLayout = new();

        [Header("Selection")]
        [SerializeField] private SelectionFsmOwner selectionOwner;

        private MainFlowFsm flowFsm;
        private SelectionFsm selectionFsm;
        private SelectionPresentation presentation;
        private RewardScreenSubState subState = RewardScreenSubState.Hidden;
        private readonly List<Transform> spawnedOptions = new();
        private int pendingSelectedIndex = -1;
        private int fallOffPending;

        public RewardScreenSubState SubState => subState;
        public int LastSelectedIndex => pendingSelectedIndex;
        public bool LastSelectionWasPass =>
            pendingSelectedIndex >= 0 && pendingSelectedIndex == RewardOptionCount;

        public void Bind(
            MainFlowFsm fsm,
            SelectionFsm selection,
            SelectionPresentation selectionPresentation)
        {
            flowFsm = fsm;
            selectionFsm = selection;
            presentation = selectionPresentation ?? selectionOwner?.Presentation;
            selectionOwner?.Bind(selectionFsm);
        }

        public void OnScreenEntered()
        {
            if (rewardPanel != null)
            {
                rewardPanel.SetActive(true);
            }

            subState = RewardScreenSubState.Entering;
            selectionFsm?.ActivateChannel(SelectionChannel.General);
            selectionFsm.InputLocked = true;

            ClearSpawnedOptions();
            BuildOptions();
            WireGeneralOptions();
            WireInputRelays();

            var entranceTargets = new List<Transform>(spawnedOptions);
            presentation?.PlayGeneralEntrance(entranceTargets, OnEnterComplete);
        }

        public void OnScreenExited()
        {
            subState = RewardScreenSubState.Hidden;
            selectionFsm?.Deactivate();
            selectionFsm?.ForceReset();
            presentation?.ForceGeneralReset();
            ClearSpawnedOptions();

            if (rewardPanel != null)
            {
                rewardPanel.SetActive(false);
            }
        }

        public void HandleOptionConfirmed(int index)
        {
            if (subState != RewardScreenSubState.Ready || index < 0 || index >= spawnedOptions.Count)
            {
                return;
            }

            subState = RewardScreenSubState.Confirming;
            selectionFsm.InputLocked = true;
            pendingSelectedIndex = index;

            Transform selected = spawnedOptions[index];
            int baselineSorting = 10 + index;
            presentation?.PlayGeneralConfirm(
                selected,
                null,
                baselineSorting,
                spawnedOptions.Count,
                PlayFallOffForUnselected);
        }

        private void OnEnterComplete()
        {
            subState = RewardScreenSubState.Ready;
            if (selectionFsm != null)
            {
                selectionFsm.InputLocked = false;
            }
        }

        private void BuildOptions()
        {
            Transform root = optionsRoot != null ? optionsRoot : transform;
            int totalCount = RewardOptionCount + 1;

            for (var i = 0; i < totalCount; i++)
            {
                Vector3 localPosition = fanLayout.GetLocalPosition(i, totalCount);
                float rotationZ = fanLayout.GetRotationZ(i);
                Transform actor = SelectionOptionVisual.CreatePreviewCard(
                    root,
                    i,
                    standUiSelectionPrefab,
                    localPosition,
                    rotationZ,
                    10 + i);

                string label = i < RewardOptionCount
                    ? HarnessRewardLabels[i]
                    : PassLabel;
                SelectionOptionVisual.ApplyLabel(actor, label);
                spawnedOptions.Add(actor);
            }
        }

        private void WireGeneralOptions()
        {
            if (presentation == null)
            {
                return;
            }

            presentation?.SetGeneralSimpleHover(false);
            var actors = new List<SelectionPresentation.GeneralOption>(spawnedOptions.Count);
            for (var i = 0; i < spawnedOptions.Count; i++)
            {
                Transform option = spawnedOptions[i];
                if (option == null)
                {
                    continue;
                }

                actors.Add(new SelectionPresentation.GeneralOption(
                    option,
                    option.localPosition,
                    option.localEulerAngles.z,
                    10 + i));
            }

            presentation.SetGeneralOptions(actors);
        }

        private void WireInputRelays()
        {
            for (var i = 0; i < spawnedOptions.Count; i++)
            {
                Transform option = spawnedOptions[i];
                if (option == null)
                {
                    continue;
                }

                var relay = option.GetComponent<SelectionOptionInputRelay>();
                if (relay == null)
                {
                    relay = option.gameObject.AddComponent<SelectionOptionInputRelay>();
                }

                relay.OptionIndex = i;
                relay.Owner = selectionOwner;
            }
        }

        private void PlayFallOffForUnselected()
        {
            if (presentation == null || spawnedOptions.Count == 0)
            {
                OnConfirmComplete();
                return;
            }

            int selectedIndex = pendingSelectedIndex;
            fallOffPending = 0;

            for (var i = 0; i < spawnedOptions.Count; i++)
            {
                if (i == selectedIndex)
                {
                    continue;
                }

                Transform option = spawnedOptions[i];
                if (option == null)
                {
                    continue;
                }

                fallOffPending++;
                float rotationZ = fanLayout.GetRotationZ(i);
                presentation.PlayGeneralFallOff(
                    option,
                    i,
                    selectedIndex,
                    rotationZ,
                    OnSingleFallOffComplete);
            }

            if (fallOffPending == 0)
            {
                OnConfirmComplete();
            }
        }

        private void OnSingleFallOffComplete()
        {
            fallOffPending--;
            if (fallOffPending <= 0)
            {
                OnConfirmComplete();
            }
        }

        private void OnConfirmComplete()
        {
            subState = RewardScreenSubState.Done;
            flowFsm?.RequestTransition(MainFlowTransition.ConfirmReward);
        }

        private void ClearSpawnedOptions()
        {
            SelectionOptionVisual.DestroyActors(spawnedOptions);
            spawnedOptions.Clear();
        }
    }
}
