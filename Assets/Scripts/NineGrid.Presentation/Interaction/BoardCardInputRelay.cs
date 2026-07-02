using NineGrid.Presentation.Bridge;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 棋盘卡 Collider → <see cref="BoardInteractionFsm"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BoardCardInputRelay : MonoBehaviour
    {
        [SerializeField] private InGameInteractionCoordinator coordinator;
        [SerializeField] private TableNineActorBinding binding;

        private void Awake()
        {
            binding ??= GetComponent<TableNineActorBinding>();
            InteractionColliderUtility.EnsureCollider2D(gameObject);
        }

        private void OnMouseEnter()
        {
            if (!TryResolveCardUid(out int cardUid))
            {
                return;
            }

            InGameInteractionCoordinator owner = ResolveCoordinator();
            if (owner == null)
            {
                return;
            }

            if (owner.Mode == InGameInteractionMode.ItemBoardTarget)
            {
                owner.Board.NotifyItemTargetHover(cardUid, transform);
                return;
            }

            owner.Board.NotifyCardHover(cardUid, transform);
        }

        private void OnMouseExit()
        {
            if (!TryResolveCardUid(out int cardUid))
            {
                return;
            }

            InGameInteractionCoordinator owner = ResolveCoordinator();
            if (owner == null)
            {
                return;
            }

            if (owner.Mode == InGameInteractionMode.ItemBoardTarget)
            {
                owner.Board.NotifyItemTargetHoverExit(cardUid);
                return;
            }

            owner.Board.NotifyCardHoverExit(cardUid);
        }

        private void OnMouseDown()
        {
            if (!TryResolveCardUid(out int cardUid))
            {
                return;
            }

            InGameInteractionCoordinator owner = ResolveCoordinator();
            if (owner == null)
            {
                return;
            }

            if (owner.Mode == InGameInteractionMode.ItemBoardTarget)
            {
                owner.Board.NotifyItemTargetPress(cardUid, transform);
                return;
            }

            owner.Board.NotifyPress(cardUid, transform);
        }

        private void OnMouseDrag()
        {
            InGameInteractionCoordinator owner = ResolveCoordinator();
            if (owner == null || owner.Mode == InGameInteractionMode.ItemBoardTarget)
            {
                return;
            }

            owner.Board.NotifyDrag(GetPointerWorldPosition());
        }

        private void OnMouseUp()
        {
            InGameInteractionCoordinator owner = ResolveCoordinator();
            if (owner == null || owner.Mode == InGameInteractionMode.ItemBoardTarget)
            {
                return;
            }

            owner.Board.NotifyRelease();
        }

        private bool TryResolveCardUid(out int cardUid)
        {
            binding ??= GetComponent<TableNineActorBinding>();
            cardUid = binding != null ? binding.CardUid : 0;
            return cardUid > 0;
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
