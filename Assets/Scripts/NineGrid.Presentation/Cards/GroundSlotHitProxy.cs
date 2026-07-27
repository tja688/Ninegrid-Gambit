using NineGrid.Flow;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 空槽点击代理：由 GroundFieldView 在运行时动态装配到 slotN 锚点。
    /// 由 <see cref="PointerHitRouter"/> 轮询驱动（不再使用 OnMouse*）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GroundSlotHitProxy : MonoBehaviour, IPointerHitTarget
    {
        private const int TypePriority = 10;

        [Tooltip("1-based 格位编号。留空时 Awake 从节点名 slotN 解析。")]
        [SerializeField] private int slotNumber;

        private BoxCollider2D _collider;

        public int SlotNumber => slotNumber;

        public Collider2D HitCollider => _collider != null ? _collider : (_collider = GetComponent<BoxCollider2D>());

        public int HitSortOrder => 0;

        public int HitTypePriority => TypePriority;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            if (slotNumber < GroundSlotTopology.MinSlot || slotNumber > GroundSlotTopology.MaxSlot)
            {
                if (CardSlotAnchorUtility.TryParseSlotIndex(name, out var parsed))
                {
                    slotNumber = parsed;
                }
            }
        }

        private void OnEnable()
        {
            PointerHitRegistry.Register(this);
        }

        private void OnDisable()
        {
            PointerHitRegistry.Unregister(this);
        }

        public void Configure(int slot, Vector2 hitBoxSize)
        {
            slotNumber = slot;
            _collider ??= GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.size = hitBoxSize;
                _collider.isTrigger = false;
            }
        }

        public void SetHitEnabled(bool enabled)
        {
            if (_collider != null)
            {
                _collider.enabled = enabled;
            }
        }

        public void HandlePointerEnter()
        {
        }

        public void HandlePointerExit()
        {
        }

        public void HandlePointerDown()
        {
            if (PresentationInputGates.BoardSelectModeActive)
            {
                return;
            }

            if (slotNumber < GroundSlotTopology.MinSlot || slotNumber > GroundSlotTopology.MaxSlot)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return;
            }

            // 轴一互斥只认 MainlineBusy（经 IntentIntake）；勿再轮询 FieldBusy/HandBusy。
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null && hand.IsDragging)
            {
                return;
            }

            field.OnEmptySlotClicked(slotNumber);
        }
    }
}
