using NineGrid.Flow;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地面：九宫格作为单一 <see cref="IPointerHitTarget"/> 注册；
    /// 命中权威为场景格位 <see cref="BoxCollider2D"/>，按世界点解析格号（ADR-0023 / #101）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundFieldHitSurface : MonoBehaviour, IPointerHitTarget, IMultiColliderPointerHitTarget
    {
        private BoxCollider2D[] _slotColliders =
            new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];

        private int _resolvedSlot;
        private Collider2D _resolvedCollider;

        public Collider2D HitCollider =>
            _resolvedCollider != null
                ? _resolvedCollider
                : FindAnyEnabledCollider();

        public int HitSortOrder => 0;

        public int HitTypePriority => PointerHitSurfacePriorities.Field;

        public int ResolvedSlot => _resolvedSlot;

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
        }

        /// <summary>绑定场景九格命中框；不写 size / offset。</summary>
        public void BindSlotColliders(BoxCollider2D[] slotColliders)
        {
            _slotColliders = slotColliders
                ?? new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];
            _resolvedSlot = 0;
            _resolvedCollider = null;
        }

        /// <summary>世界点落入哪一格命中框（仅已启用的框）。</summary>
        public bool TryResolveSlotAtWorld(Vector2 worldXY, out int slot)
        {
            for (slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var collider = GetSlotCollider(slot);
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (collider.OverlapPoint(worldXY))
                {
                    _resolvedSlot = slot;
                    _resolvedCollider = collider;
                    return true;
                }
            }

            slot = 0;
            _resolvedSlot = 0;
            _resolvedCollider = null;
            return false;
        }

        public bool TryOverlapScreenPoint(Camera camera, Vector2 screen, out Vector3 world)
        {
            world = default;
            if (camera == null)
            {
                return false;
            }

            var planeZ = ResolvePlaneZ();
            if (!TryScreenToWorldOnPlane(camera, screen, planeZ, out world))
            {
                return false;
            }

            return TryResolveSlotAtWorld(new Vector2(world.x, world.y), out _);
        }

        public void HandlePointerEnter()
        {
        }

        public void HandlePointerExit()
        {
            _resolvedSlot = 0;
            _resolvedCollider = null;
        }

        public void HandlePointerDown()
        {
            if (PresentationInputGates.BoardSelectModeActive
                || PresentationInputGates.BattleUiOverlayActive)
            {
                return;
            }

            if (!GroundSlotTopology.IsValidSlot(_resolvedSlot))
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return;
            }

            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null && hand.IsDragging)
            {
                return;
            }

            field.OnEmptySlotClicked(_resolvedSlot);
        }

        private BoxCollider2D GetSlotCollider(int slot)
        {
            if (_slotColliders == null
                || slot < 0
                || slot >= _slotColliders.Length)
            {
                return null;
            }

            return _slotColliders[slot];
        }

        private Collider2D FindAnyEnabledCollider()
        {
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                var collider = GetSlotCollider(slot);
                if (collider != null && collider.enabled)
                {
                    return collider;
                }
            }

            return null;
        }

        private float ResolvePlaneZ()
        {
            var any = FindAnyEnabledCollider();
            return any != null ? any.transform.position.z : transform.position.z;
        }

        private static bool TryScreenToWorldOnPlane(
            Camera camera,
            Vector2 screen,
            float planeZ,
            out Vector3 world)
        {
            var depth = camera.WorldToScreenPoint(new Vector3(0f, 0f, planeZ)).z;
            world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world.z = planeZ;
            return true;
        }
    }
}
