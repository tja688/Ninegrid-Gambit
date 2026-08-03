using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 遗留空槽 Hit 壳：格位命中改由 <see cref="GroundFieldHitSurface"/> 单一表面承接（ADR-0023 / #101）。
    /// 仍可被旧场景残留；不再注册 Router，也不再写命中框 size / offset。
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

        /// <summary>仅记录格号；场景 collider 尺寸权威，禁止写 size / offset。</summary>
        public void Configure(int slot)
        {
            slotNumber = slot;
            _collider ??= GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.isTrigger = false;
            }
        }

        /// <summary>遗留 API：命中恒启用，由场地面统一承接（ADR-0023）。</summary>
        public void SetHitEnabled(bool enabled)
        {
            _collider ??= GetComponent<BoxCollider2D>();
            if (_collider != null)
            {
                _collider.enabled = true;
            }
        }
    }
}
