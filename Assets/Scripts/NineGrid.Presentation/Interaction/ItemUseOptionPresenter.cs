using System;
using System.Collections.Generic;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 局内道具选项覆盖层（如 stat_boost 三选一），复用 <see cref="SelectionFsm"/> General 通道。
    /// </summary>
    public sealed class ItemUseOptionPresenter
    {
        private readonly SelectionFanLayout mFanLayout = new();
        private readonly List<Transform> mSpawnedOptions = new();

        private SelectionFsm mSelectionFsm;
        private SelectionPresentation mPresentation;
        private SelectionFsmOwner mSelectionOwner;
        private Transform mOptionsRoot;
        private GameObject mOptionPrefab;
        private ItemUseRequirement mRequirement;
        private int mPendingItemUid;
        private Action<int, string> mOnConfirmed;
        private Action mOnCancelled;

        public bool IsActive { get; private set; }

        public void Bind(
            SelectionFsm selectionFsm,
            SelectionPresentation presentation,
            SelectionFsmOwner selectionOwner,
            Transform optionsRoot,
            GameObject optionPrefab)
        {
            mSelectionFsm = selectionFsm;
            mPresentation = presentation;
            mSelectionOwner = selectionOwner;
            mOptionsRoot = optionsRoot;
            mOptionPrefab = optionPrefab;
        }

        public void Show(
            int itemUid,
            ItemUseRequirement requirement,
            Action<int, string> onConfirmed,
            Action onCancelled)
        {
            if (requirement == null || requirement.Kind != ItemUseRequirementKind.Option)
            {
                onCancelled?.Invoke();
                return;
            }

            Hide();
            mPendingItemUid = itemUid;
            mRequirement = requirement;
            mOnConfirmed = onConfirmed;
            mOnCancelled = onCancelled;
            IsActive = true;

            mSelectionFsm?.ActivateChannel(SelectionChannel.General);
            if (mSelectionFsm != null)
            {
                mSelectionFsm.InputLocked = true;
            }

            BuildOptions(requirement);
            WireGeneralOptions();
            WireInputRelays();

            var entranceTargets = new List<Transform>(mSpawnedOptions);
            mPresentation?.SetGeneralSimpleHover(true);
            mPresentation?.PlayGeneralEntrance(entranceTargets, OnEnterComplete);
        }

        public void Hide()
        {
            if (!IsActive && mSpawnedOptions.Count == 0)
            {
                return;
            }

            IsActive = false;
            mPendingItemUid = 0;
            mRequirement = null;
            mOnConfirmed = null;
            mOnCancelled = null;

            mPresentation?.ForceGeneralReset();
            mPresentation?.SetGeneralOptions(Array.Empty<SelectionPresentation.GeneralOption>());
            SelectionOptionVisual.DestroyActors(mSpawnedOptions);
            mSelectionFsm?.ForceReset();
            mSelectionFsm?.Deactivate();
        }

        public bool TryHandleOptionConfirmed(int index)
        {
            if (!IsActive || index < 0 || index >= mSpawnedOptions.Count)
            {
                return false;
            }

            if (mSelectionFsm != null)
            {
                mSelectionFsm.InputLocked = true;
            }

            string optionId = mRequirement.ResolveOptionId(index);
            int itemUid = mPendingItemUid;
            var confirmed = mOnConfirmed;
            Hide();
            confirmed?.Invoke(itemUid, optionId);
            return true;
        }

        private void OnEnterComplete()
        {
            if (mSelectionFsm != null)
            {
                mSelectionFsm.InputLocked = false;
            }
        }

        private void BuildOptions(ItemUseRequirement requirement)
        {
            Transform root = mOptionsRoot != null ? mOptionsRoot : null;
            if (root == null)
            {
                return;
            }

            int count = requirement.OptionLabels.Count;
            for (var i = 0; i < count; i++)
            {
                Vector3 localPosition = mFanLayout.GetLocalPosition(i, count);
                float rotationZ = mFanLayout.GetRotationZ(i);
                Transform actor = SelectionOptionVisual.CreatePreviewCard(
                    root,
                    i,
                    mOptionPrefab,
                    localPosition,
                    rotationZ,
                    30 + i);

                SelectionOptionVisual.ApplyLabel(actor, requirement.OptionLabels[i]);
                mSpawnedOptions.Add(actor);
            }
        }

        private void WireGeneralOptions()
        {
            if (mPresentation == null)
            {
                return;
            }

            var actors = new List<SelectionPresentation.GeneralOption>(mSpawnedOptions.Count);
            for (var i = 0; i < mSpawnedOptions.Count; i++)
            {
                Transform option = mSpawnedOptions[i];
                if (option == null)
                {
                    continue;
                }

                actors.Add(new SelectionPresentation.GeneralOption(
                    option,
                    option.localPosition,
                    option.localEulerAngles.z,
                    30 + i));
            }

            mPresentation.SetGeneralOptions(actors);
        }

        private void WireInputRelays()
        {
            for (var i = 0; i < mSpawnedOptions.Count; i++)
            {
                Transform option = mSpawnedOptions[i];
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
                relay.Owner = mSelectionOwner;
            }
        }
    }
}
