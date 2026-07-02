using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shell;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 局内交互协调器：Board/Hand 互斥、输入锁闸门、道具使用前置路由。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameInteractionCoordinator : MonoBehaviour
    {
        private readonly BoardInteractionFsm mBoardFsm = new();
        private readonly HandInteractionFsm mHandFsm = new();
        private readonly List<Transform> mHandActors = new();
        private readonly ItemUseOptionPresenter mItemUseOptionPresenter = new();

        private CommandGateway mGateway;
        private IArchitecture mArchitecture;
        private Collider2D mUseZoneCollider;
        private SelectionFsm mSelectionFsm;
        private Transform mItemOptionRoot;
        private InGameInteractionMode mMode = InGameInteractionMode.Board;
        private bool mInputLocked;

        public BoardInteractionFsm Board => mBoardFsm;
        public HandInteractionFsm Hand => mHandFsm;
        public InGameInteractionMode Mode => mMode;
        public bool InputLocked => mInputLocked;
        public IReadOnlyList<Transform> HandActors => mHandActors;
        public bool IsItemUseSessionActive =>
            mMode == InGameInteractionMode.ItemBoardTarget
            || mMode == InGameInteractionMode.ItemOption;

        public void Install(
            CommandGateway gateway,
            IArchitecture architecture,
            BoardCardHoverPresenter boardHoverPresenter,
            HandCardDragPresenter handDragPresenter,
            HandCardReturnPresenter handReturnPresenter,
            HandLayoutPresenter handLayoutPresenter,
            TableNineViewRegistry viewRegistry,
            Collider2D useZoneCollider)
        {
            mGateway = gateway;
            mArchitecture = architecture;
            mUseZoneCollider = useZoneCollider;

            mBoardFsm.Bind(
                boardHoverPresenter,
                gateway,
                architecture,
                viewRegistry,
                IsBoardInputAllowed,
                IsItemTargetInputAllowed);
            mBoardFsm.ItemTargetsConfirmed += OnItemTargetsConfirmed;
            mBoardFsm.ItemTargetCancelled += OnItemTargetCancelled;

            mHandFsm.Bind(
                handDragPresenter,
                handReturnPresenter,
                handLayoutPresenter,
                gateway,
                architecture,
                IsHandInputAllowed,
                () => mHandActors,
                IsPointerInUseZone);

            mHandFsm.ItemUseRequested += OnItemUseRequested;
            ApplyInputLock(mGateway != null && mGateway.IsInputLocked);
        }

        public void InstallItemUseSelection(
            SelectionFsm selectionFsm,
            SelectionPresentation presentation,
            SelectionFsmOwner selectionOwner,
            Transform optionRoot,
            GameObject optionPrefab)
        {
            mSelectionFsm = selectionFsm;
            mItemOptionRoot = optionRoot;
            mItemUseOptionPresenter.Bind(
                selectionFsm,
                presentation,
                selectionOwner,
                optionRoot,
                optionPrefab);

            if (selectionFsm != null)
            {
                selectionFsm.OptionConfirmed += OnSelectionOptionConfirmed;
            }
        }

        private void OnDestroy()
        {
            mHandFsm.ItemUseRequested -= OnItemUseRequested;
            mBoardFsm.ItemTargetsConfirmed -= OnItemTargetsConfirmed;
            mBoardFsm.ItemTargetCancelled -= OnItemTargetCancelled;

            if (mSelectionFsm != null)
            {
                mSelectionFsm.OptionConfirmed -= OnSelectionOptionConfirmed;
            }

            mItemUseOptionPresenter.Hide();
        }

        private void Update()
        {
            if (mGateway == null)
            {
                return;
            }

            bool locked = mGateway.IsInputLocked;
            if (locked != mInputLocked)
            {
                ApplyInputLock(locked);
            }

            if (mMode == InGameInteractionMode.ItemBoardTarget && Input.GetKeyDown(KeyCode.Escape))
            {
                CancelItemUseSession();
            }
        }

        public void SetHandActors(IReadOnlyList<Transform> actors)
        {
            mHandActors.Clear();
            if (actors == null)
            {
                return;
            }

            for (var i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null)
                {
                    mHandActors.Add(actors[i]);
                }
            }
        }

        public void NotifyHandEngaged()
        {
            if (mMode == InGameInteractionMode.Board)
            {
                mBoardFsm.ForceReset();
                mMode = InGameInteractionMode.HandItem;
            }
        }

        public void TryReleaseHandMode()
        {
            if (mMode == InGameInteractionMode.HandItem && mHandFsm.State == HandInteractionState.Idle)
            {
                mMode = InGameInteractionMode.Board;
            }
        }

        public bool IsBoardInputAllowed()
        {
            return !mInputLocked && mMode == InGameInteractionMode.Board;
        }

        public bool IsHandInputAllowed()
        {
            return !mInputLocked && mMode == InGameInteractionMode.HandItem;
        }

        public bool IsItemTargetInputAllowed()
        {
            return !mInputLocked && mMode == InGameInteractionMode.ItemBoardTarget;
        }

        public void CancelItemUseSession()
        {
            if (mMode == InGameInteractionMode.ItemBoardTarget)
            {
                mBoardFsm.CancelItemTargetSelection();
                return;
            }

            if (mMode == InGameInteractionMode.ItemOption)
            {
                mItemUseOptionPresenter.Hide();
                mMode = InGameInteractionMode.Board;
            }
        }

        private void OnItemUseRequested(int itemUid)
        {
            if (mArchitecture == null || itemUid <= 0)
            {
                return;
            }

            string defId = ResolveItemDefId(itemUid);
            ItemUseRequirement requirement = ItemUseRequirementResolver.Resolve(defId);

            switch (requirement.Kind)
            {
                case ItemUseRequirementKind.None:
                    DispatchUseItem(itemUid, null, null);
                    break;
                case ItemUseRequirementKind.Option:
                    BeginOptionSelection(itemUid, requirement);
                    break;
                case ItemUseRequirementKind.BoardTarget:
                    if (!ItemUseTargetValidator.HasAnyValidTarget(mArchitecture, requirement))
                    {
                        return;
                    }

                    BeginBoardTargetSelection(itemUid, requirement);
                    break;
            }
        }

        private void BeginOptionSelection(int itemUid, ItemUseRequirement requirement)
        {
            if (mItemOptionRoot == null || mSelectionFsm == null)
            {
                mMode = InGameInteractionMode.Board;
                return;
            }

            mMode = InGameInteractionMode.ItemOption;
            mHandFsm.ForceReset();
            mBoardFsm.ForceReset();

            mItemUseOptionPresenter.Show(
                itemUid,
                requirement,
                OnItemOptionConfirmed,
                OnItemOptionCancelled);
        }

        private void BeginBoardTargetSelection(int itemUid, ItemUseRequirement requirement)
        {
            mMode = InGameInteractionMode.ItemBoardTarget;
            mHandFsm.ForceReset();
            mBoardFsm.BeginItemTargetSelection(itemUid, requirement);
        }

        private void OnItemOptionConfirmed(int itemUid, string optionId)
        {
            mMode = InGameInteractionMode.Board;
            DispatchUseItem(itemUid, null, optionId);
        }

        private void OnItemOptionCancelled()
        {
            mMode = InGameInteractionMode.Board;
        }

        private void OnItemTargetsConfirmed(int itemUid, IReadOnlyList<int> selectedCardUids)
        {
            mMode = InGameInteractionMode.Board;
            DispatchUseItem(itemUid, selectedCardUids, null);
        }

        private void OnItemTargetCancelled()
        {
            if (mMode == InGameInteractionMode.ItemBoardTarget)
            {
                mMode = InGameInteractionMode.Board;
            }
        }

        private void OnSelectionOptionConfirmed(int index)
        {
            if (mMode != InGameInteractionMode.ItemOption)
            {
                return;
            }

            mItemUseOptionPresenter.TryHandleOptionConfirmed(index);
        }

        private void DispatchUseItem(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            if (mGateway == null || itemUid <= 0)
            {
                return;
            }

            mGateway.Send(new UseItemCommand(itemUid, selectedCardUids, selectedOption));
            mMode = InGameInteractionMode.Board;
        }

        private string ResolveItemDefId(int itemUid)
        {
            if (mArchitecture == null)
            {
                return string.Empty;
            }

            var registry = mArchitecture.GetModel<CardRegistry>();
            if (!registry.TryGet(itemUid, out CardInstance card))
            {
                return string.Empty;
            }

            return card.DefId ?? string.Empty;
        }

        private void ApplyInputLock(bool locked)
        {
            mInputLocked = locked;
            mBoardFsm.InputLocked = locked;
            mHandFsm.InputLocked = locked;

            if (locked)
            {
                mItemUseOptionPresenter.Hide();
                mBoardFsm.ForceReset();
                mHandFsm.ForceReset();
                mMode = InGameInteractionMode.Board;
            }
        }

        private bool IsPointerInUseZone(Vector3 worldPosition)
        {
            if (mUseZoneCollider == null)
            {
                return true;
            }

            return mUseZoneCollider.OverlapPoint(worldPosition);
        }
    }
}
