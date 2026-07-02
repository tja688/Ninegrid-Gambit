using System.Collections.Generic;
using NineGrid.Presentation.Bridge;
using NineGrid.Presentation.Orchestration;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 局内交互协调器：Board/Hand 互斥、输入锁闸门、Relay 统一入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameInteractionCoordinator : MonoBehaviour
    {
        private readonly BoardInteractionFsm mBoardFsm = new();
        private readonly HandInteractionFsm mHandFsm = new();
        private readonly List<Transform> mHandActors = new();

        private CommandGateway mGateway;
        private IArchitecture mArchitecture;
        private Collider2D mUseZoneCollider;
        private InGameInteractionMode mMode = InGameInteractionMode.Board;
        private bool mInputLocked;

        public BoardInteractionFsm Board => mBoardFsm;
        public HandInteractionFsm Hand => mHandFsm;
        public InGameInteractionMode Mode => mMode;
        public bool InputLocked => mInputLocked;
        public IReadOnlyList<Transform> HandActors => mHandActors;

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
                IsBoardInputAllowed);

            mHandFsm.Bind(
                handDragPresenter,
                handReturnPresenter,
                handLayoutPresenter,
                gateway,
                architecture,
                IsHandInputAllowed,
                () => mHandActors,
                IsPointerInUseZone);

            mHandFsm.ItemUseDispatched += OnItemUseDispatched;
            ApplyInputLock(mGateway != null && mGateway.IsInputLocked);
        }

        private void OnDestroy()
        {
            mHandFsm.ItemUseDispatched -= OnItemUseDispatched;
        }

        private void Update()
        {
            if (mGateway == null)
            {
                return;
            }

            bool locked = mGateway.IsInputLocked;
            if (locked == mInputLocked)
            {
                return;
            }

            ApplyInputLock(locked);
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

        private void OnItemUseDispatched(int itemUid)
        {
            mMode = InGameInteractionMode.Board;
        }

        private void ApplyInputLock(bool locked)
        {
            mInputLocked = locked;
            mBoardFsm.InputLocked = locked;
            mHandFsm.InputLocked = locked;

            if (locked)
            {
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
