using NineGrid.Core;
using NineGrid.Presentation.Bridge;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 空棋盘格锚点 Collider → <see cref="BoardInteractionFsm"/>（ClickEmpty）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BoardSlotInputRelay : MonoBehaviour
    {
        [SerializeField] private int boardSlotIndex = 1;
        [SerializeField] private InGameInteractionCoordinator coordinator;

        public int BoardSlotIndex
        {
            get => boardSlotIndex;
            set => boardSlotIndex = value;
        }

        private void Awake()
        {
            InteractionColliderUtility.EnsureCollider2D(gameObject);
        }

        private void OnMouseEnter()
        {
            ResolveCoordinator()?.Board.NotifySlotHover(ResolveSlot(), transform);
        }

        private void OnMouseExit()
        {
            ResolveCoordinator()?.Board.NotifySlotHoverExit(ResolveSlot());
        }

        private void OnMouseDown()
        {
            ResolveCoordinator()?.Board.NotifySlotPress(ResolveSlot(), transform);
        }

        private void OnMouseDrag()
        {
            ResolveCoordinator()?.Board.NotifyDrag(GetPointerWorldPosition());
        }

        private void OnMouseUp()
        {
            ResolveCoordinator()?.Board.NotifyRelease();
        }

        private SlotId ResolveSlot()
        {
            if (boardSlotIndex < SlotId.MinBoardIndex || boardSlotIndex > SlotId.MaxBoardIndex)
            {
                return SlotId.None;
            }

            return SlotId.Board(boardSlotIndex);
        }

        private InGameInteractionCoordinator ResolveCoordinator()
        {
            if (coordinator != null)
            {
                return coordinator;
            }

            return NineGridSceneBootstrap.Current?.InteractionCoordinator;
        }

        private static Vector3 GetPointerWorldPosition()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Vector3.zero;
            }

            Vector3 pointer = Input.mousePosition;
            pointer.z = -camera.transform.position.z;
            return camera.ScreenToWorldPoint(pointer);
        }
    }
}
