using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 空槽点击代理：由 GroundFieldManagerSingleton 在运行时动态装配到 slotN 锚点。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class GroundSlotHitProxy : MonoBehaviour
    {
        [Tooltip("1-based 格位编号。留空时 Awake 从节点名 slotN 解析。")]
        [SerializeField] private int slotNumber;

        private BoxCollider2D _collider;

        public int SlotNumber => slotNumber;

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

        private void OnMouseDown()
        {
            if (CombatHitSink.BoardSelectModeActive)
            {
                return;
            }

            if (slotNumber < GroundSlotTopology.MinSlot || slotNumber > GroundSlotTopology.MaxSlot)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || field.IsBusy)
            {
                return;
            }

            var hand = CardHandManagerSingleton.Instance;
            if (hand != null && (hand.IsBusy || hand.IsDragging))
            {
                return;
            }

            field.OnEmptySlotClicked(slotNumber);
        }
    }
}
