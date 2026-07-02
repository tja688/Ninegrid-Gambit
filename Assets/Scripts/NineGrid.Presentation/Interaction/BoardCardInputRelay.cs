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

            ResolveCoordinator()?.Board.NotifyCardHover(cardUid, transform);
        }

        private void OnMouseExit()
        {
            if (!TryResolveCardUid(out int cardUid))
            {
                return;
            }

            ResolveCoordinator()?.Board.NotifyCardHoverExit(cardUid);
        }

        private void OnMouseDown()
        {
            if (!TryResolveCardUid(out int cardUid))
            {
                return;
            }

            ResolveCoordinator()?.Board.NotifyPress(cardUid, transform);
        }

        private void OnMouseDrag()
        {
            ResolveCoordinator()?.Board.NotifyDrag(GetPointerWorldPosition());
        }

        private void OnMouseUp()
        {
            ResolveCoordinator()?.Board.NotifyRelease();
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
