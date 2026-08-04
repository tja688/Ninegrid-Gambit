using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地面：九宫格作为单一 <see cref="IPointerHitTarget"/> 注册；
    /// 命中权威为场景格位 <see cref="BoxCollider2D"/>，按世界点解析格号后查认领者（ADR-0023）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundFieldHitSurface : MonoBehaviour, IPointerHitTarget, IMultiColliderPointerHitTarget
    {
        private BoxCollider2D[] _slotColliders =
            new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];

        private int _resolvedSlot;
        private Collider2D _resolvedCollider;
        private int _hoverTipGeneration;
        private SlotClaimant _hoveredClaimant;

        public Collider2D HitCollider =>
            _resolvedCollider != null
                ? _resolvedCollider
                : FindAnyEnabledCollider();

        public int HitSortOrder => 0;

        public int HitTypePriority => PointerHitSurfacePriorities.Field;

        public int ResolvedSlot => _resolvedSlot;

        /// <summary>
        /// 右键详述：当前格认领者为场地卡时返回其 <see cref="ManagedCard"/>（ADR-0023）。
        /// 房间图标等非卡认领者返回 false，避免误开详述。
        /// </summary>
        public bool TryResolveInspectCard(out ManagedCard card)
        {
            card = null;
            if (!GroundSlotTopology.IsValidSlot(_resolvedSlot))
            {
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !field.TryGetSlotClaimant(_resolvedSlot, out var claimant))
            {
                return false;
            }

            if (claimant.Owner is GroundCardHitProxy proxy)
            {
                card = proxy.BoundCardOrNull;
                return card != null;
            }

            return false;
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
            ClearHoverState();
        }

        /// <summary>绑定场景九格命中框；不写 size / offset。</summary>
        public void BindSlotColliders(BoxCollider2D[] slotColliders)
        {
            _slotColliders = slotColliders
                ?? new BoxCollider2D[GroundSlotTopology.MaxSlot + 1];
            _resolvedSlot = 0;
            _resolvedCollider = null;
        }

        /// <summary>世界点落入哪一格命中框（九框恒开）。</summary>
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
            ApplyHoverForResolvedSlot();
        }

        public void HandlePointerExit()
        {
            ClearHoverState();
            _resolvedSlot = 0;
            _resolvedCollider = null;
        }

        /// <inheritdoc cref="IMultiColliderPointerHitTarget.RefreshPointerHover"/>
        public void RefreshPointerHover()
        {
            ApplyHoverForResolvedSlot();
        }

        private void ApplyHoverForResolvedSlot()
        {
            if (!GroundSlotTopology.IsValidSlot(_resolvedSlot))
            {
                ClearHoverState();
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            SlotClaimant claimant = null;
            var hasClaim = field != null && field.TryGetSlotClaimant(_resolvedSlot, out claimant);
            if (!hasClaim)
            {
                ClearHoverState();
                return;
            }

            if (ReferenceEquals(_hoveredClaimant, claimant))
            {
                return;
            }

            ClearHoverState();
            _hoveredClaimant = claimant;
            if (!string.IsNullOrEmpty(claimant.BriefTipText))
            {
                var presenter = BoardBriefTipPresenter.EnsureExists();
                _hoverTipGeneration = presenter.ShowHover(claimant.BriefTipText);
            }

            claimant.HoverEnter?.Invoke();
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

            if (field.TryGetSlotClaimant(_resolvedSlot, out var claimant))
            {
                claimant.Activate?.Invoke();
                return;
            }

            field.OnEmptySlotClicked(_resolvedSlot);
        }

        private void ClearHoverState()
        {
            if (_hoveredClaimant != null)
            {
                _hoveredClaimant.HoverExit?.Invoke();
                _hoveredClaimant = null;
            }

            if (_hoverTipGeneration <= 0)
            {
                return;
            }

            var presenter = BoardBriefTipPresenter.InstanceOrNull();
            if (presenter != null)
            {
                presenter.ClearHover(_hoverTipGeneration);
            }

            _hoverTipGeneration = 0;
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
