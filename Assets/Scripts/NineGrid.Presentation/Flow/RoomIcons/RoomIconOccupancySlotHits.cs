using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 软占变更后刷新格位命中框（ADR-0023 后仅为 Ensure 恒开，不再按软占关框）。
    /// </summary>
    public static class RoomIconOccupancySlotHits
    {
        public static void Refresh(IArchitecture arch = null)
        {
            var geometry = arch?.GetSystem<IGroundFieldGeometrySystem>()
                           ?? NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>()
                           ?? NineGridArchitecture.Current?.GetSystem<IGroundFieldGeometrySystem>();
            geometry?.RefreshSlotHitColliders();
        }
    }
}
