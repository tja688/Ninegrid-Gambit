using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// ADR-0024：落格只对齐世界位置，不成为 <c>GroundAnchors/slotN</c> 子物体。
    /// </summary>
    public static class BoardSlotWorldPlacement
    {
        public static bool TryAlignToSlot(
            Transform target,
            IGroundFieldGeometrySystem geometry,
            int slot)
        {
            if (target == null || geometry == null)
            {
                return false;
            }

            var anchor = geometry.GetGroundAnchor(slot);
            if (anchor == null)
            {
                return false;
            }

            target.position = anchor.position;
            return true;
        }
    }
}
