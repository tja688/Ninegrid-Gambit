using NineGrid.Presentation.Bridge;
using UnityEngine;

namespace NineGrid.Presentation.Interaction
{
    /// <summary>
    /// 手牌道具 Collider → <see cref="HandInteractionFsm"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class HandCardInputRelay : MonoBehaviour
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
            if (!TryResolveItemUid(out int itemUid))
            {
                return;
            }

            InGameInteractionCoordinator owner = ResolveCoordinator();
            owner?.NotifyHandEngaged();
            owner?.Hand.NotifyHover(itemUid, transform);
        }

        private void OnMouseExit()
        {
            if (!TryResolveItemUid(out int itemUid))
            {
                return;
            }

            InGameInteractionCoordinator owner = ResolveCoordinator();
            owner?.Hand.NotifyHoverExit(itemUid);
            owner?.TryReleaseHandMode();
        }

        private void OnMouseDown()
        {
            if (!TryResolveItemUid(out int itemUid))
            {
                return;
            }

            InGameInteractionCoordinator owner = ResolveCoordinator();
            owner?.NotifyHandEngaged();
            owner?.Hand.NotifyPress(itemUid, transform, GetPointerWorldPosition());
        }

        private void OnMouseDrag()
        {
            InGameInteractionCoordinator owner = ResolveCoordinator();
            owner?.NotifyHandEngaged();
            owner?.Hand.NotifyDrag(GetPointerWorldPosition());
        }

        private void OnMouseUp()
        {
            InGameInteractionCoordinator owner = ResolveCoordinator();
            owner?.NotifyHandEngaged();
            owner?.Hand.NotifyRelease(GetPointerWorldPosition());
            owner?.TryReleaseHandMode();
        }

        private bool TryResolveItemUid(out int itemUid)
        {
            binding ??= GetComponent<TableNineActorBinding>();
            itemUid = binding != null ? binding.CardUid : 0;
            return itemUid > 0;
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
